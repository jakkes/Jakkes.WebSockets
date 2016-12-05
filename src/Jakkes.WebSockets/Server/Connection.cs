using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Jakkes.WebSockets.Server
{

    public delegate void MessageReceivedEventHandler(Connection source, string data);

    public class Connection : IDisposable
    {

        public event MessageReceivedEventHandler MessageReceived;

        private TcpClient _conn;
        private NetworkStream _stream;
        private byte[] buffer = new byte[4096];
        private string message = string.Empty;
        private OpCode previousOpcode;

        public Connection(TcpClient conn)
        {
            if (!conn.Connected)
                throw new ArgumentException("The connection is closed.");
            _conn = conn;
            _stream = conn.GetStream();
            Task.Run(() => Read());
        }
        public void Send(string message)
        {
            Send(Encoding.UTF8.GetBytes(message), OpCode.TextFrame);
        }
        public void Send(byte[] data, OpCode code)
        {
            List<byte> bytes = new List<byte>();
            bytes.Add((byte)(0x80 + code));
            if (data.Length <= 125)
                bytes.Add((byte)(data.Length));
            else if (data.Length <= 65535)
            {
                bytes.Add((byte)126);
                bytes.Add((byte)((0xFF & (data.Length >> 8))));
                bytes.Add((byte)(0xFF & data.Length));
            }
            else
            {
                bytes.Add((byte)127);
                for (int i = 0; i < 8; i++)
                    bytes.Add((byte)(0xFF & ((long)(data.Length) >> (8 * (7 - i)))));
            }
            _stream.Write(bytes.ToArray(), 0, bytes.Count);
            _stream.Write(data,0,data.Length);
            _stream.Flush();
        }
        private async void Read()
        {
            int read = await _stream.ReadAsync(buffer, 0, buffer.Length);
            long length = 0;
            
            // Read first bit
            bool FIN = buffer[0] >> 7 == 1;

            // Check message type and if it's a ping.
            int opcode = buffer[0] & 0xF;
            if(opcode == (int)OpCode.Ping)
            {
                Pong();
                return;
            }

            // Read bit 2-4
            if (((buffer[0] >> 3) & 0xF) != 0) 
                throw new NotImplementedException("Extensions not implemented");


            // Get length of message
            int len = buffer[1] & 0x7F;
            if (len < 126)
            {
                length = len;
            }
            else if (len == 126)
                length = (buffer[2] << 8) + buffer[3];
            else
                for (int i = 0; i < 8; i++)
                    length += (buffer[2 + i]) << (8 * (7 - i));

            // Check if the message is masked
            if (buffer[1] >> 7 != 1)
                throw new NotImplementedException("No mask present. Connection must close.");

            // Get masking key
            byte[] mask = new byte[4];
            int offset = 0;
            if (len < 126) offset = 2;
            else if (len == 126) offset = 4;
            else offset = 10;
            for (int j = 0; j < 4; offset++, j++)
                mask[j] = buffer[offset];

            switch (opcode)
            {
                case (int)OpCode.ContinuationFrame:
                    switch (previousOpcode)
                    {
                        case OpCode.TextFrame:
                            HandleTextMessage(FIN, length, mask, offset);
                            break;
                    }
                    break;


                case (int)OpCode.TextFrame:
                    previousOpcode = OpCode.TextFrame;
                    HandleTextMessage(FIN, length, mask, offset);
                    break;
                    
                default:
                    throw new NotImplementedException("Unknown opcode. Connection must close.");

            }
            Task.Run(() => Read());
        }
        private void Pong()
        {
            throw new NotImplementedException("Pong not implemented");
        }
        private void HandleTextMessage(bool FIN, long length, byte[] mask, int offset)
        {
            if(buffer.Length - offset > length)
                message += Encoding.UTF8.GetString(UnMask(buffer,offset,(int)length,mask,0));
            else
            {
                long count = 0;
                message += Encoding.UTF8.GetString(UnMask(buffer, offset, buffer.Length - offset, mask,0));
                count += buffer.Length - offset;
                while(count < length)
                {
                    int read = _stream.Read(buffer, 0, buffer.Length);
                    message += Encoding.UTF8.GetString(UnMask(buffer, 0, read, mask, (int)(count % 4)));
                    count += read;
                }
            }
            if (FIN)
            {
                onMessageReceived(message);
                MessageReceived?.Invoke(this, message);
                message = string.Empty;
            }
        }
        private byte[] UnMask(byte[] buffer, int offset, int count, byte[] mask, int shift)
        {
            var re = new byte[count];
            for(int i = 0; i < count; i++)
                re[i] = (byte)(buffer[i + offset] ^ mask[(i+shift) % 4]);
            return re;
        }
        protected void onMessageReceived(string data)
        {

        }
        public void Close()
        {

        }
        public void Dispose()
        {
            throw new NotImplementedException();
        }
        
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Jakkes.WebSockets.Server
{

    public delegate void MessageReceivedEventHandler(Connection source, string data);
    public delegate void BinaryReceivedEventHandler(Connection source, byte[] data);

    public class Connection : IDisposable
    {

        public event MessageReceivedEventHandler MessageReceived;
        public event BinaryReceivedEventHandler BinaryReceived;

        private TcpClient _conn;
        private NetworkStream _stream;
        private string message = string.Empty;
        private List<byte> binary = new List<byte>();
        private OpCode currentOpCode;

        internal Connection(TcpClient conn)
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
        protected void onMessageReceived(string data)
        {

        }
        protected void onBinaryReceived(byte[] data)
        {

        }
        private async Task<Frame> GetFrameInfo()
        {
            var re = new Frame();

            // Read until first 2 bytes are read
            int read = 0;
            byte[] buff = new byte[2];
            read += await _stream.ReadAsync(buff, 0, buff.Length);
            while (read < buff.Length)
                read += await _stream.ReadAsync(buff, read, buff.Length - read);

            // Check message is masked
            if ((buff[1] >> 7) != 1)
                throw new UnmaskedMessageException();
            
            // Check RSV1,2,3 are set 0
            if ((buff[0] & 0x70) != 0)
                throw new NotImplementedException("RSV1, RSV2, RSV3 must be set to 0.");

            // Extract opcode
            re.OpCode = (OpCode)(buff[0] & 0xF);
            
            // Extract FIN
            re.FIN = (buff[0] >> 7) == 1;

            // Extract length
            int len = buff[1] & 0x7F;
            if (len < 126) re.Length = len;
            else if (len == 126)
                re.Length = (_stream.ReadByte() << 8) + _stream.ReadByte();
            else
                for (int i = 0; i < 8; i++)
                    re.Length += _stream.ReadByte() << (8 * (7 - i));

            // Extract mask
            re.Mask = new byte[4];
            _stream.Read(re.Mask, 0, 4);

            // Extract payload
            re.Payload = new byte[re.Length];
            _stream.Read(re.Payload, 0, re.Payload.Length);

            return re;
        }
        private async void Read()
        {
            try
            {
                var frame = await GetFrameInfo();

                switch (frame.OpCode)
                {
                    case OpCode.TextFrame:
                        HandleIncomingText(frame);
                        break;
                    case OpCode.BinaryFrame:
                        HandleIncomingBinary(frame);
                        break;
                    case OpCode.ConnectionClose:
                        HandleCloseRequest();
                        break;
                    case OpCode.Ping:
                        Pong();
                        break;
                    case OpCode.ContinuationFrame:
                        if (currentOpCode == OpCode.TextFrame)
                            HandleIncomingText(frame);
                        else
                            HandleIncomingBinary(frame);
                        break;
                    default:
                        Close();
                        break;
                }
            }
            catch (UnmaskedMessageException) { Close("Received an unmasked message from the client."); }

            Task.Run(() => Read());
        }
        private void HandleIncomingBinary(Frame frame)
        {
            binary.AddRange(frame.UnmaskedData);
            if (frame.FIN)
            {
                onBinaryReceived(binary.ToArray());
                BinaryReceived?.Invoke(this, binary.ToArray());
                binary.Clear();
            }
            else
                currentOpCode = OpCode.BinaryFrame;
        }

        private void HandleIncomingText(Frame frame)
        {
            message += Encoding.UTF8.GetString(frame.UnmaskedData);
            if (frame.FIN)
            {
                onMessageReceived(message);
                MessageReceived?.Invoke(this, message);
                message = string.Empty;
            }
            else
                currentOpCode = OpCode.TextFrame;
        }

        private void HandleCloseRequest()
        {
            throw new NotImplementedException();
        }
        private void Pong()
        {
            throw new NotImplementedException("Pong not implemented");
        }
        public void Close()
        {
            Close("");
        }
        public void Close(string message)
        {
            throw new NotImplementedException("Close");
        }
        public void Dispose()
        {
            throw new NotImplementedException();
        }
        
        private class Frame
        {
            public bool FIN { get; set; }
            public long Length { get; set; }
            public byte[] Mask { get; set; }
            public byte[] Payload { get; set; }
            public OpCode OpCode { get; set; }
            public byte[] UnmaskedData
            {
                get
                {
                    if(_unmaskedData == null)
                    {
                        var d = new byte[Payload.Length];
                        for (int i = 0; i < Payload.Length; i++)
                            d[i] = (byte)(Payload[i] ^ Mask[i % 4]);
                        _unmaskedData = d;
                    }
                    return _unmaskedData;
                }
            }

            private byte[] _unmaskedData;
        }
    }

    public class UnmaskedMessageException : Exception
    {

    }

}

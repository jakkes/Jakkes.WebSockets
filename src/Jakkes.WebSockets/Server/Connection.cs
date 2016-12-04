using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Jakkes.WebSockets.Server
{

    public delegate void MessageReceivedEventHandler(Connection source, string data);
    public delegate void BinaryReceivedEventHandler(Connection source, byte[] data);

    public class Connection : IDisposable
    {

        private TcpClient _conn;
        private NetworkStream _stream;
        private byte[] buffer = new byte[4096];
        private string message = string.Empty;

        public Connection(TcpClient conn)
        {
            if (!conn.Connected)
                throw new ArgumentException("The connection is closed.");
            _conn = conn;
            _stream = conn.GetStream();
            Task.Run(() => Read());
        }
        private async void Read()
        {
            int read = await _stream.ReadAsync(buffer, 0, buffer.Length);
            long length = 0;

            // Check if the message is masked
            if (buffer[1] >> 7 != 1)
                throw new NotImplementedException("No mask present. Connection must close.");

            // Read first bit
            bool FIN = buffer[0] >> 7 == 1;

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

            // Get masking key
            byte[] mask = new byte[4];
            int n = 0;
            if (len < 126) n = 2;
            else if (len == 126) n = 4;
            else n = 10;
            for (int j = 0; j < 4; n++, j++)
                mask[j] = buffer[n];


            // Check message type
            int opcode = buffer[0] & 0xF;

            switch (opcode)
            {
                case (int)OpCode.TextFrame:
                    HandleTextMessage(FIN, length, mask, n);
                    break;
                default:
                    throw new NotImplementedException("Unknown opcode. Connection must close.");

            }

            buffer = new byte[buffer.Length];
            Task.Run(() => Read());
        }

        private void HandleTextMessage(bool FIN, long length, byte[] mask, int offset)
        {
            message += Encoding.UTF8.GetString(buffer, offset, buffer.Length - offset);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        private enum OpCode
        {
            TextFrame = 0x1,
            BinaryFrame = 0x2,
            ConnectionClose = 0x8,
            Ping = 0x9,
            Pong = 0xA
        }
    }
}

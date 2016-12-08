using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jakkes.WebSockets.Server
{

    public delegate void MessageReceivedEventHandler(Connection source, string data);
    public delegate void BinaryReceivedEventHandler(Connection source, byte[] data);
    public delegate void WebSocketConnectionClosedEventHandler(Connection source);
    public delegate void WebSocketStateChangedEventHandler(Connection source, WebSocketState state);

    public class Connection
    {

        public event MessageReceivedEventHandler MessageReceived;
        public event BinaryReceivedEventHandler BinaryReceived;
        public event WebSocketConnectionClosedEventHandler Closed;
        public event WebSocketStateChangedEventHandler StateChanged;

        public WebSocketState State
        {
            get { return _state; }
            private set
            {
                _state = value;
                onStateChanged(_state);
                StateChanged?.Invoke(this, _state);
            }
        }
        public string ID { get { return _id; } }

        private string _id = Guid.NewGuid().ToString();
        private WebSocketState _state = WebSocketState.Open;
        private TcpClient _conn;
        private NetworkStream _stream;
        private string message = string.Empty;
        private List<byte> binary = new List<byte>();
        private OpCode currentOpCode;

        protected virtual void onMessageReceived(string data) { }
        protected virtual void onBinaryReceived(byte[] data) { }
        protected virtual void onStateChanged(WebSocketState state) { }
        protected virtual void onClosed() { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn">A TCP connection that has been upgraded to a websocket connection.</param>
        public Connection(TcpClient conn)
        {
            if (!conn.Connected)
                throw new ArgumentException("The connection is closed.");
            _conn = conn;
            _stream = conn.GetStream();
            Task.Run(() => Read());
        }
        /// <summary>
        /// Sends a string
        /// </summary>
        /// <param name="text">Message</param>
        public void Send(string text)
        {
            _send(Encoding.UTF8.GetBytes(message), OpCode.TextFrame);
        }
        /// <summary>
        /// Sends binary
        /// </summary>
        /// <param name="binary">Data</param>
        public void Send(byte[] binary)
        {
            _send(binary, OpCode.BinaryFrame);
        }
        private void _send(byte[] data, OpCode code)
        {
            if (State != WebSocketState.Open)
                throw new ConnectionClosedException();

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
            long count = 0;
            while(count < re.Length)
                count += _stream.Read(re.Payload, (int)count, (int)(re.Length - count));

            return re;
        }
        private async void Read()
        {
            if (State != WebSocketState.Open)
                return;
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
                        HandleCloseRequest(frame);
                        break;
                    case OpCode.Ping:
                        Pong(frame);
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
            catch (UnmaskedMessageException) { Close(); }
            
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
        private void HandleCloseRequest(Frame frame)
        {
            if (State != WebSocketState.Closing)
                _send(frame.UnmaskedData, OpCode.ConnectionClose);
            _shutdown();
        }
        private void Pong(Frame frame)
        {
            _send(frame.UnmaskedData, OpCode.Pong);
        }
        private void _shutdown()
        {
            _stream.Dispose();
            _conn.Dispose();
            State = WebSocketState.Closed;
            onClosed();
            Closed?.Invoke(this);
        }
        /// <summary>
        /// Closes the connection. Connection enters closing state until it closes when Closed is invoked.
        /// </summary>
        public void Close()
        {
            _send(new byte[0], OpCode.ConnectionClose);
            State = WebSocketState.Closing;
        }
        /// <summary>
        /// Closes the connection.
        /// </summary>
        /// <param name="hardquit">Set true if the connection should be abandoned instead of performing a clean exit.</param>
        public void Close(bool hardquit)
        {
            Close();
            if (hardquit)
                _shutdown();
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

    public enum WebSocketState
    {
        Open,
        Closed,
        Closing
    }

    public class UnmaskedMessageException : Exception { }
    public class ConnectionClosedException : Exception { }

}

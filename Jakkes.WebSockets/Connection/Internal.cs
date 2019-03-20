using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace Jakkes.WebSockets
{
    public delegate void StateChangedEventHandler(Connection conn, State state);
    public delegate void TextReceivedEventHandler(Connection conn, string text);
    public delegate void BinaryReceivedEventHandler(Connection conn, byte[] binary);
    public delegate void ServerMessageSentEventHandler(Connection conn, Message msg);
    

    public partial class Connection 
    {
        private TcpClient _conn;
        private NetworkStream _stream;

        private State _state = State.Closed;
        public State State
        {
            get { return _state; }
            private set
            {
                if(value != _state)
                {
                    _state = value;
                    var i = Task.Run(() => StateChanged?.Invoke(this, new StateChangedEventArgs(value)));
                }
            }
        }
        private OpCode currentOpCode;
        private List<byte> binary = new List<byte>();
        private AsyncPrioQueue<Message> _queue = new AsyncPrioQueue<Message>();
        private static Regex http_response = new Regex("HTTP/1.1 (\\d+) Switching Protocols");
        // True if the object was created manually as a websocket client.
        private bool _client = true;
        private Random random = new Random();
        private Task _readTask;
        private Task _writeTask;

        private CancellationTokenSource cancellationTokenSource;

        private Connection()
        {
            _conn = new TcpClient();
        }
        internal Connection(TcpClient conn)
        {
            _conn = conn;
            _stream = conn.GetStream();
            _client = false;    
            _start();
        }
        private void _start() {
            State = State.Open;

            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            
            _readTask = Task.Run(() => _readWorker(token));
            _writeTask = Task.Run(() => _sendWorker(token));

        }
        private void SendPrioritized(Message msg)
        {
            _queue.EnqueuePrioritized(msg);
        }
        private async Task _sendWorker(CancellationToken cancellationToken)
        {
            while (true)
            {
                if (State == State.Closed || cancellationToken.IsCancellationRequested) {
                    return;
                }
                var msgTask = _queue.DequeueAsync();

                while (true) {
                    
                    if (State == State.Closed || cancellationToken.IsCancellationRequested) {
                        return;
                    }

                    if (msgTask.IsCompleted) {
                        var msg = await msgTask.ConfigureAwait(false);
                        _writeToStream(msg);

                        var ignore = Task.Run(() => MessageSent?.Invoke(this, new MessageSentEventArgs(msg)));
                        
                        if (msg.OnSuccess != null) {
                            var a = Task.Run(msg.OnSuccess);
                        }

                        if (msg.opCode == OpCode.ConnectionClose) {
                            return;
                        }
                        break;
                    } else {
                        await Task.Delay(10).ConfigureAwait(false);
                    }
                }
            }
        }
        private Frame _convertToFrame(Message msg) {
            var frame = new Frame();

            frame.FIN = true;
            frame.OpCode = msg.opCode;
            frame.Length = msg.Data.LongLength;

            if (_client) {
                frame.Masked = true;
                frame.Mask = new byte[4];
                random.NextBytes(frame.Mask);
                frame.Payload = new byte[msg.Data.LongLength];
                for (int i = 0; i < msg.Data.LongLength; i++)
                    frame.Payload[i] = (byte)(msg.Data[i] ^ frame.Mask[i % 4]);
            } else {
                frame.Masked = false;
                frame.Payload = msg.Data;
            }

            return frame;
        }
        private void _writeToStream(Message msg)
        {
            if (State != State.Open)
                if (State != State.Closing && msg.opCode != OpCode.ConnectionClose)
                    throw new ConnectionClosedException();

            var frame = _convertToFrame(msg);

            int masked_byte = frame.Masked ? 0x80 : 0x00;  // Mask or not mask

            List<byte> bytes = new List<byte>();

            // Add opcode and FIN
            if (frame.FIN)
                bytes.Add((byte)(0x80 + frame.OpCode));
            else
                bytes.Add((byte)frame.OpCode);

            // Add payload length
            if (frame.Length <= 125)
                bytes.Add((byte)(frame.Length + masked_byte));
            else if (frame.Length <= 65535)
            {
                bytes.Add((byte)(126 + masked_byte));
                bytes.Add((byte)((0xFF & (frame.Length >> 8))));
                bytes.Add((byte)(0xFF & frame.Length));
            }
            else
            {
                bytes.Add((byte)(127 + masked_byte));
                for (int i = 0; i < 8; i++)
                    bytes.Add((byte)(0xFF & ((long)(frame.Length) >> (8 * (7 - i)))));
            }
            
            if (frame.Masked){
                bytes.AddRange(frame.Mask);
            }

            bytes.AddRange(frame.Payload);

            _stream.Write(bytes.ToArray(), 0, bytes.Count);
        }
        private async Task _readWorker(CancellationToken cancellationToken)
        {
            while (true) {
                
                if (State == State.Closed || cancellationToken.IsCancellationRequested) {
                    return;
                }

                var frameTask = GetFrameAsync();
                while (true) {
                    if (State == State.Closed || cancellationToken.IsCancellationRequested) {
                        return;
                    }

                    if (frameTask.IsCompleted) {
                        try
                        {
                            var frame = await frameTask.ConfigureAwait(false);
                            HandleFrame(frame);
                            break;
                        }
                        catch (UnmaskedMessageException) {
                            var ignorewarning = Task.Run(() => _shutdown());
                            return;
                        }
                    } else {
                        await Task.Delay(10).ConfigureAwait(false);
                    }
                }
            }
        }
        public void Close()
        {
            State = State.Closing;
            var msg = new Message(
                new byte[0],
                OpCode.ConnectionClose,
                async () => { 
                    await FlushAsync();
                },
                null
            );
            SendPrioritized(msg);
        }
        public void Kill()
        {
            _shutdown();
        }
        private void HandleFrame(Frame frame)
        {
            if (frame.FIN)
            {
                switch (frame.OpCode)
                {
                    case OpCode.TextFrame:
                        var ignore1 = Task.Run(() => TextReceived?.Invoke(this, new TextReceivedEventArgs(Encoding.UTF8.GetString(frame.UnmaskedData))));
                        break;
                    case OpCode.BinaryFrame:
                        var ignore2 = Task.Run(() => BinaryReceived?.Invoke(this, new BinaryReceivedEventArgs(frame.UnmaskedData)));
                        break;
                    case OpCode.ConnectionClose:
                        HandleCloseRequest(frame);
                        break;
                    case OpCode.Ping:
                        Pong(frame);
                        break;
                    case OpCode.ContinuationFrame:
                        binary.AddRange(frame.UnmaskedData);
                        if (currentOpCode == OpCode.TextFrame) {
                            var ignore3 = Task.Run(() => TextReceived?.Invoke(this, new TextReceivedEventArgs(Encoding.UTF8.GetString(binary.ToArray()))));
                        } else {
                            var ignore4 = Task.Run(() => BinaryReceived?.Invoke(this, new BinaryReceivedEventArgs(binary.ToArray())));
                        }
                        break;
                    default:
                        Close();
                        break;
                }
                binary.Clear();
            }
            else
            {
                binary.AddRange(frame.UnmaskedData);
                currentOpCode = frame.OpCode;
            }
        }
        private void Pong(Frame frame)
        {
            SendPrioritized(new Message(frame.UnmaskedData,OpCode.Pong));
        }
        private void HandleCloseRequest(Frame frame)
        {
            
            if (State == State.Open){
                State = State.Closing;
                var msg = new Message(
                    frame.UnmaskedData,
                    OpCode.ConnectionClose,
                    async () => {
                        await FlushAsync();
                        var ignorewarning = Task.Run(() =>_shutdown());
                    },
                    null
                );
                SendPrioritized(msg);
            } else if (State == State.Closing) {
                Task.Run(() => _shutdown());
            }
        }
        private void _shutdown(){
            State = State.Closed;
            cancellationTokenSource.Cancel();

            

            Task.WaitAll(_readTask, _writeTask);

            
            _stream.Dispose();
            _conn.Dispose();
        }
        private async Task<Frame> GetFrameAsync()
        {
            var re = new Frame();

            // Read until first 2 bytes are read
            int read = 0;
            byte[] buff = new byte[2];
            try {
                while (read < buff.Length) {
                    read += await _stream.ReadAsync(buff, read, buff.Length - read);
                }
            } catch (ObjectDisposedException) {
                return null;
            }
            // Check message is masked
            if ((buff[1] >> 7) != 1) {
                if (!_client) {
                    _shutdown();
                    throw new UnmaskedMessageException("Client sent an unmasked message. Connection was closed.");
                }
                re.Masked = false;
            } else {
                re.Masked = true;
            }

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
            if (re.Masked) {
                re.Mask = new byte[4];
                for (int i = 0; i < 4; i++)
                    re.Mask[i] = (byte) _stream.ReadByte();
            }

            // Extract payload
            re.Payload = new byte[re.Length];
            long count = 0;
            while (count < re.Length)
                count += _stream.Read(re.Payload, (int)count, (int)(re.Length - count));

            return re;
        }
        private class Frame
        {
            public bool FIN { get; set; }
            public bool Masked { get; set; }
            public long Length { get; set; }
            public byte[] Mask { get; set; }
            public byte[] Payload { get; set; }
            public OpCode OpCode { get; set; }
            public byte[] UnmaskedData
            {
                get
                {   
                    if (!Masked)
                        return Payload;
                    
                    if (_unmaskedData == null)
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
}
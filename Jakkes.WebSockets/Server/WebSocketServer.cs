using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Security.Cryptography;
using System.Text;
using System.IO;

using Jakkes.WebSockets;

namespace Jakkes.WebSockets.Server
{

    public class WebSocketServer
    {
        public State State
        {
            get { return _state; }
            private set
            {
                if (value != _state)
                {
                    _state = value;
                    var i = Task.Run(() => {
                        StateChanged?.Invoke(this, new StateChangedEventArgs(State));
                        OnStateChanged(new StateChangedEventArgs(State));
                    });
                }
            }
        }
        private State _state = State.Closed;
        private Receiver _receiver;
        public string Path { get; private set; }
        public int Port { get; private set; }

        public int Count => _connections.Count;
        public IEnumerable<Connection> Connections { get { return _connections.ToArray(); } }
        private HashSet<Connection> _connections = new HashSet<Connection>();
        public WebSocketServer(int port) : this(port, "/") { }
        public WebSocketServer(int port, string path)
        {
            Path = path;
            Port = port;
        }

        public void Start()
        {
            _receiver = Receiver.GetReceiver(Port);
            _receiver.RegisterServer(this);

            State = State.Open;
        }
        public void Close() => Close(-1);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="timeout">Milliseconds before a KILL-signal is sent. -1 will send no KILL-signal</param>
        public void Close(int timeout)
        {
            State = State.Closing;

            if (Connections.Any()) {
                lock (_connections) {
                    foreach (var conn in Connections)
                        conn.Close();
                }

                if (timeout > 0) {
                    Task.Run(async () =>
                    {
                        await Task.Delay(timeout);
                        if (Connections.Count() > 0)
                            Kill();
                    });
                }
            } else {
                _shutdown();
            }
        }
        public void Broadcast(Message msg)
        {
            lock (_connections) {
                foreach (var conn in Connections)
                    conn.Send(msg);
            }
        }
        public void Broadcast(string text) => Broadcast(new Message(text));
        public void Broadcast(byte[] binary) => Broadcast(new Message(binary));
        public async Task FlushAsync() {
            List<Task> tasks = new List<Task>(_connections.Count);
            lock (_connections)
            {
                foreach (var conn in _connections)
                    tasks.Add(conn.FlushAsync());
            }

            await Task.WhenAll(tasks);
        }
        internal void RegisterClient(Connection client)
        {
            client.StateChanged += Client_StateChanged;
            client.TextReceived += Client_TextReceived;
            client.BinaryReceived += Client_BinaryReceived;
            
            lock (_connections)
                _connections.Add(client);

            var i = Task.Run(() => {
                ClientConnected?.Invoke(this, new ClientConnectedEventArgs(client));
                OnClientConnected(client);
            });
        }

        private void Client_TextReceived(object sender, TextReceivedEventArgs e)
        {
            var args = new ServerReceivedTextEventArgs(sender as Connection, e);
            TextReceived?.Invoke(this, args);
            OnTextReceived(args);
        }

        private void Client_BinaryReceived(object sender, BinaryReceivedEventArgs e)
        {
            var args = new ServerReceivedBinaryEventArgs(sender as Connection, e);
            BinaryReceived?.Invoke(this, args);
            OnBinaryReceived(args);
        }

        private void Client_StateChanged(object conn, StateChangedEventArgs args)
        {
            if (args.State == State.Closed)
            {
                lock (_connections)
                    _connections.Remove((Connection)conn);

                if (State == State.Closing && _connections.Count == 0)
                    _shutdown();
            }
        }
        public void Kill()
        {
            lock (_connections) {
                foreach (var conn in Connections)
                    conn.Kill();
            }
        }
        private void _shutdown()
        {
            State = State.Closed;
            _receiver.UnregisterServer(this);
        }
        public event EventHandler<StateChangedEventArgs> StateChanged;
        protected virtual void OnStateChanged(StateChangedEventArgs e) { }
        public event EventHandler<ClientConnectedEventArgs> ClientConnected;
        protected virtual void OnClientConnected(Connection client) { }
        public event EventHandler<ServerReceivedTextEventArgs> TextReceived;
        protected virtual void OnTextReceived(ServerReceivedTextEventArgs args) { }
        public event EventHandler<ServerReceivedBinaryEventArgs> BinaryReceived;
        protected virtual void OnBinaryReceived(ServerReceivedBinaryEventArgs args) { }
        
    }

    public class ClientConnectedEventArgs {
        private readonly Connection _client;
        
        public Connection Client => _client;
        
        public ClientConnectedEventArgs(Connection client)
        {
            _client = client;
        }
    }

    public class ServerReceivedTextEventArgs : EventArgs {
        private readonly Connection _source;
        
        public Connection Source => _source;
        private readonly string _text;
        
        public string Text => _text;
        
        public ServerReceivedTextEventArgs(Connection source, TextReceivedEventArgs args)
        {
            _source = source;
            _text = args.Text;
        }
    }

    public class ServerReceivedBinaryEventArgs : EventArgs {
        private readonly Connection _source;
        
        public Connection Source => _source;
        private readonly byte[] _binary;
        
        public byte[] Binary => _binary;
        
        public ServerReceivedBinaryEventArgs(Connection source, BinaryReceivedEventArgs args)
        {
            _source = source;
            _binary = args.Binary;
        }
    }
}

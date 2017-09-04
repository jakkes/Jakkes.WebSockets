using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Threading;

namespace Jakkes.WebSockets.Server
{
    public delegate void ServerStateChangedEventHandler(WebSocketServer server, ServerState newState);
    public delegate void ClientConnectedEventHandler(WebSocketServer server, Connection client);

    public class WebSocketServer
    {
        public ServerState State
        {
            get { return _state; }
            private set
            {
                if(value != _state)
                {
                    _state = value;
                    StateChanged?.Invoke(this, State);
                    OnStateChanged(State);
                }
            }
        }
        private ServerState _state = ServerState.Closed;

        private TcpListener _server;

        public IEnumerable<Connection> Connections { get { return _connections.ToArray(); } }
        private HashSet<Connection> _connections = new HashSet<Connection>();
        
        public WebSocketServer(int port) : this(IPAddress.Any, port) { }
        public WebSocketServer(IPAddress ip, int port)
        {
            _server = new TcpListener(ip, port);
        }

        public void Start()
        {
            _server.Start();

            #pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(() => awaitConnection());
            
            State = ServerState.Open;
        }
        public void Close() => Close(-1);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="timeout">Milliseconds before a KILL-signal is sent. -1 will send no KILL-signal</param>
        public void Close(int timeout)
        {
            State = ServerState.Closing;
            foreach (var conn in Connections)
                conn.Close();

            if (timeout > 0)
                Task.Run(() =>
                {
                    Thread.Sleep(timeout);
                    if (Connections.Count() > 0)
                        Kill();
                });
        }
        public void Broadcast(ServerMessage msg)
        {
            foreach (var conn in Connections)
                conn.Send(msg);
        }
        public void Broadcast(string text) => Broadcast(new ServerMessage(text));
        public void Broadcast(byte[] binary) => Broadcast(new ServerMessage(binary));
        private async void awaitConnection()
        {
            var conn = await _server.AcceptTcpClientAsync();
            var handle = Task.Run(() => handleConnection(conn));
            #pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(() => awaitConnection());
            await handle;
        }

        private void handleConnection(TcpClient conn)
        {
            try
            {
                var client = new Connection(conn);
                client.StateChanged += Client_StateChanged;
                _connections.Add(client);

                ClientConnected?.Invoke(this, client);
                OnClientConnected(client);
            } catch (HandshakeException ex)
            {
                Console.WriteLine("Handshake with new client failed.");
                Console.WriteLine(ex.Message);
            }
        }

        private void Client_StateChanged(Connection conn, ConnectionState state)
        {
            if (state == ConnectionState.Closed)
            {
                _connections.Remove(conn);
                if (State == ServerState.Closing && _connections.Count == 0)
                    _shutdown();
            }
        }
        public void Kill()
        {
            foreach (var conn in Connections)
                conn.Kill();
        }
        private void _shutdown()
        {
            _server.Stop();
        }
        public event ServerStateChangedEventHandler StateChanged;
        protected virtual void OnStateChanged(ServerState newState) { }
        public event ClientConnectedEventHandler ClientConnected;
        protected virtual void OnClientConnected(Connection client) { }
    }
    public enum ServerState
    {
        Open,
        Closing,
        Closed
    }
}

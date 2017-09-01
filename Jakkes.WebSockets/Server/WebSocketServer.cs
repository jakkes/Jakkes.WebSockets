using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Threading.Tasks;
using System;

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

        public IEnumerable<Connection> Connections { get { return _connections.AsEnumerable(); } }
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
                _connections.Add(client);

                ClientConnected?.Invoke(this, client);
                OnClientConnected(client);
            } catch (HandshakeException ex)
            {
                Console.WriteLine("Handshake with new client failed.");
                Console.WriteLine(ex.Message);
            }
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

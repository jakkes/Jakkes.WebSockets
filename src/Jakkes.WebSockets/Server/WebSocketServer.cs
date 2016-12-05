using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Jakkes.WebSockets.Server
{
    public delegate void WebSocketClientConnectedEventHandler(WebSocketServer source, Connection conn);
    public class WebSocketServer : IDisposable
    {

        public event WebSocketClientConnectedEventHandler ClientConnected;

        private TcpListener _server;
        private List<Connection> _connections = new List<Connection>();
        public WebSocketServer(int port) : this(IPAddress.Any, port) { }
        public WebSocketServer(IPAddress ip, int port)
        {
            _server = new TcpListener(ip, port);
        }
        public void Start()
        {
            _server.Start();
            Task.Run(() => Listen());
        }
        
        public void Broadcast(string message)
        {
            lock (_connections)
                foreach (var conn in _connections)
                    conn.Send(message);
        }
        private async void Listen()
        {
            var conn = await _server.AcceptTcpClientAsync();
            HandleConnection(conn);
            Task.Run(() => Listen());
        }
        private void HandleConnection(TcpClient conn)
        {
            var stream = conn.GetStream();
            StreamReader reader = new StreamReader(stream);
            StreamWriter writer = new StreamWriter(stream);
            Dictionary<string, string> dict = new Dictionary<string, string>();

            string line;
            while (!string.IsNullOrEmpty(line = reader.ReadLine()))
            {
                if (line.StartsWith("Sec-WebSocket-Key"))
                    dict.Add("Key", line.Split(':')[1].Trim());
            }

            if (!dict.ContainsKey("Key"))
                throw new NotImplementedException("Failed to receive the key necessary to upgrade the connection.");
            string acceptKey = Convert.ToBase64String(
                                    SHA1.Create().ComputeHash(
                                        Encoding.UTF8.GetBytes(
                                            dict["Key"] + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
                                        )
                                    )
                                );

            string response = "HTTP/1.1 101 Switching Protocols" + Environment.NewLine;
            response += "Upgrade: websocket" + Environment.NewLine;
            response += "Connection: Upgrade" + Environment.NewLine;
            response += "Sec-WebSocket-Accept: " + acceptKey + Environment.NewLine + Environment.NewLine;
            Console.WriteLine(acceptKey);
            stream.Write(Encoding.UTF8.GetBytes(response), 0, Encoding.UTF8.GetByteCount(response));
            stream.Flush();

            Connection socket = new Connection(conn);
            ClientConnected?.Invoke(this, socket);
            onClientConnect(socket);
            _connections.Add(socket);
        }
        protected void onClientConnect(Connection conn) { }
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}

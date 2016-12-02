using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace Jakkes.WebSockets
{
    public class WebSocketServer
    {
        private TcpListener _server;
        public WebSocketServer(int port) : this(IPAddress.Any, port)
        {

        }
        public WebSocketServer(IPAddress ip, int port)
        {
            _server = new TcpListener(ip, port);
        }
        public void Start() => _server.Start();
    }
}

using System;
using Jakkes.WebSockets.Server;

namespace ChatExample
{
    public class ChatServer : WebSocketServer
    {

        public ChatServer(int port) : base(port)
        {
            
        }

        protected override void OnClientConnected(Connection client)
        {
            client.TextReceived += (o, e) =>
            {
                Broadcast(e);
            };
        }

        static void Main(string[] args)
        {
            var srv = new ChatServer(8080);
            srv.Start();
            Console.WriteLine("Started chat server on port 8080");
        }
    }
}

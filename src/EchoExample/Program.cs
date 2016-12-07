using Jakkes.WebSockets.Server;
using System;

namespace EchoExample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var srv = new WebSocketServer(8080);
            srv.ClientConnected += Srv_ClientConnected;
            srv.Start();
            Console.ReadLine();
        }

        private static void Srv_ClientConnected(WebSocketServer source, Connection conn)
        {
            conn.MessageReceived += Conn_MessageReceived;
        }

        private static void Conn_MessageReceived(Connection source, string data)
        {
            source.Send(data);
        }
    }
}

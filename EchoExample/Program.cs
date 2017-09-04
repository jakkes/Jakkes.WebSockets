using System;
using Jakkes.WebSockets.Server;

namespace EchoExample
{
    class Program
    {
        static void Main(string[] args)
        {
            int port = 8080;
            var srv = new Server(port);
            srv.Start();
            Console.WriteLine("Started echo service on port " + port);
            Console.Read();
        }
    }

    class Server : WebSocketServer{
        public Server(int port) : base(port){

        }

        protected override void OnClientConnected(Connection client){
            client.TextReceived += (o,e) => {
                Console.WriteLine(e);
                System.Threading.Thread.Sleep(500);
                client.Send(e);
            };
        }
    }
}
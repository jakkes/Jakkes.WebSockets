using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jakkes.WebSockets.Server;

namespace ex1
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Connection conn;
            var srv = new WebSocketServer(8080);
            srv.Start();
            srv.ClientConnected += (o, e) =>
            {
                conn = e;
                conn.MessageReceived += (o1, e1) =>
                {
                    Console.WriteLine(e1);
                    o1.Send(e1);
                };
            };
            Console.Read();

        }
    }
}

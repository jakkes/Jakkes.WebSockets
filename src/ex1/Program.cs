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

            var srv = new WebSocketServer(8080);
            srv.Start();
            Console.Read();

        }
    }
}

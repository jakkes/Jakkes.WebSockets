using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Jakkes.WebSockets.Server;
using System.Threading;

namespace Jakkes.WebSockets.Test.Server { 

    public class tests
    {
        [Fact]
        public void CloseTest()
        {
            Console.WriteLine("Starting CloseTest");
            var srv = new WebSocketServer(8080);
            srv.Start();
            Thread.Sleep(15000);
            srv.Close();
        }

        [Fact]
        public void KillTest()
        {
            Console.WriteLine("Starting KillTest");
            var srv = new WebSocketServer(8081);
            srv.Start();
            Thread.Sleep(15000);
            srv.Kill();
        }
    }
}

using System;
using Xunit;
using Jakkes.WebSockets;
using Jakkes.WebSockets.Server;
using System.Linq;
using System.Threading.Tasks;

namespace Jakkes.WebSockets.Test
{
    public class PathTests
    {
        
        [Fact]
        public async Task ClientConnectsCorrectPath()
        {
            var server = Utilities.openServer(40001);
            var server1 = Utilities.openServer(40001, "/1");
            var server2 = Utilities.openServer(40001, "/2");

            var conn1 = await Connection.ConnectAsync("ws://localhost:40001");
            var conn2 = await Connection.ConnectAsync("ws://localhost:40001/1");
            var conn3 = await Connection.ConnectAsync("ws://localhost:40001/2");

            Assert.Equal(1, server.Connections.Count());
            Assert.Equal(1, server.Connections.Count());
            Assert.Equal(1, server.Connections.Count());

            await Utilities.closeServer(server);
            await Utilities.closeServer(server1);
            await Utilities.closeServer(server2);
        }
    }
}
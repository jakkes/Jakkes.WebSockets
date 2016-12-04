using Xunit;
using Jakkes.WebSockets.Server;

namespace tests
{
    public class Tests
    {
        [Fact]
        public void PassingTest()
        {
            var a = new WebSocketServer(100);
            a.Start();
            Assert.True(true);
        }
    }
}

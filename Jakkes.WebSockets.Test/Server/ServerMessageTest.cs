using Jakkes.WebSockets.Server;
using Xunit;

namespace Jakkes.WebSockets.Test.Server
{
    class ServerMessageTest
    {
        [Fact]
        public void TextMessage()
        {
            var m1 = new ServerMessage("testseteststsetsetess");
            Assert.IsType<string>(m1.Message);
            Assert.True(m1.Message == "testseteststsetsetess");

            var m2 = new ServerMessage(new byte[] { 12, 12, 12, 12, 12 });
            Assert.ThrowsAny<ServerMessage.InvalidTypeException>(() => { var s = m2.Message; });
        }
    }
}

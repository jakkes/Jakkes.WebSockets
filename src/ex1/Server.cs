using Jakkes.WebSockets.Server;

namespace ChatExample1
{
    public class Server : WebSocketServer
    {
        public Server(int port) : base(port) { }

        protected override void onMessageReceived(Connection conn, string msg)
        {
            Broadcast(msg);
        }
    }
}

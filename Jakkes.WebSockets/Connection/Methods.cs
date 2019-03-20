using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Jakkes.WebSockets {
    public partial class Connection {
        
        private static Regex urlRegex = new Regex(@"(?<protocol>ws|wss):\/\/(?<host>[a-zA-Z]+):(?<port>\d+)(?<path>\/[a-zA-Z0-9\/]+)?");

        public static async Task<Connection> ConnectAsync(string url) {

            var match = urlRegex.Match(url);

            if (!match.Success)
                throw new ArgumentException("The URL is invalid.");

            string protocol = match.Groups["protocol"].Value;
            string host = match.Groups["host"].Value;
            int port = int.Parse(match.Groups["port"].Value);

            string path = "/";
            if (match.Groups["path"].Success)
                path = match.Groups["path"].Value;

            var re = new Connection();
            await re._conn.ConnectAsync(host, port);
            re._stream = re._conn.GetStream();

            var key = await re.SendHandshakeRequestAsync(host, port, path);
            await re.ReadHandshakeResponseAsync(key);

            re._start();
            return re;
        }

        public void Send(string text) => Send(new Message(text));
        public void Send(string text, Action OnSuccess, Action OnFail)
            => Send(new Message(text,OnSuccess,OnFail));
        public void Send(byte[] binary) => Send(new Message(binary));
        public void Send(byte[] binary, Action OnSuccess, Action OnFail)
            => Send(new Message(binary,OnSuccess,OnFail));
        public void Send(Message msg)
        {
            _queue.Enqueue(msg);
        }
        public async Task FlushAsync() {
            await _stream.FlushAsync();
        }
    }
}
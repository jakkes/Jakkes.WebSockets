using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Jakkes.WebSockets {

    public partial class Connection {
        private async Task ReadHandshakeResponseAsync(string key) {
            StreamReader reader = new StreamReader(_stream);
            
            string line;

            bool http_response_matched = false;
            bool upgrade_matched = false;
            bool websocket_matched = false;
            bool key_matched = false;

            while (!string.IsNullOrEmpty((line = await reader.ReadLineAsync()))) {
                var http_response_match = http_response.Match(line);
                
                if (http_response_match.Success) {
                    if (int.Parse(http_response_match.Groups[1].ToString()) != 101) {
                        throw new HandshakeException("Status code did not equal 101.");
                    } else {
                        http_response_matched = true;
                    }
                } else {
                    var splitted = line.ToLower().Split(':');
                    if (splitted[0] == "connection" && splitted[1].Contains("upgrade")) {
                        upgrade_matched = true;
                    } else if (splitted[0] == "upgrade" && splitted[1].Contains("websocket")) {
                        websocket_matched = true;
                    } else if (splitted[0] == "sec-websocket-accept") {
                        string correctkey = Convert.ToBase64String(
                                    SHA1.Create().ComputeHash(
                                        Encoding.UTF8.GetBytes(
                                            key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
                                        )
                                    )
                                );
                        if (splitted[1].Trim() == correctkey.ToLower()) {
                            key_matched = true;
                        }
                    }
                }
            }

            if (!upgrade_matched)
                throw new HandshakeException("Handshake response missed the \"Connection: Upgrade\" keyword.");
            if (!http_response_matched)
                throw new HandshakeException("Invalid HTTP response.");
            if (!websocket_matched)
                throw new HandshakeException("Handshake response missed the \"Upgrade: Websocket\" keyword.");
            if (!key_matched)
                throw new HandshakeException("Handshake responded with an invalid key.");
        }
        private async Task<string> SendHandshakeRequestAsync(string host, int port, string path) {
            var builder = new StringBuilder();
            
            byte[] key = new byte[16];
            random.NextBytes(key);
            string keystring = System.Convert.ToBase64String(key);

            builder.AppendLine("GET " + path + " HTTP/1.1");
            builder.AppendLine("Host: " + host + ":" + port);
            builder.AppendLine("Connection: Upgrade");
            builder.AppendLine("Upgrade: websocket");
            builder.AppendLine("Sec-WebSocket-Version: 13");
            builder.AppendLine("Sec-WebSocket-Key:" + keystring);
            builder.AppendLine();

            var request = builder.ToString();

            var bytes = Encoding.UTF8.GetBytes(request);
            await _stream.WriteAsync(bytes, 0, bytes.Length);
            await _stream.FlushAsync();

            return keystring;
        }
    }

    public class HandshakeException : Exception
    {
        public HandshakeException()
        {

        }
        public HandshakeException(string msg) : base(msg)
        {

        }
    }
}
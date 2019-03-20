using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Collections;

namespace Jakkes.WebSockets.Server
{
    internal sealed partial class Receiver
    {
        private TcpListener _tcpListener;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private Task _connectionListenerTask;
        private Dictionary<string, WebSocketServer> _servers = new Dictionary<string, WebSocketServer>();
        public int Port { get; private set; }

        private Receiver(int port) {
            Port = port;
            _tcpListener = new TcpListener(IPAddress.Any, port);
            _tcpListener.Start();
            _connectionListenerTask = awaitConnectionAsync(cancellationTokenSource.Token);
        }
        private async Task awaitConnectionAsync(CancellationToken token)
        {
            while (true) {
                var connTask = _tcpListener.AcceptTcpClientAsync();

                while (true) {

                    if (token.IsCancellationRequested) {
                        return;
                    }

                    if (connTask.IsCompleted) {
                        var conn = await connTask;
                        await handleConnectionAsync(conn, token);
                        break;
                    } else {
                        await Task.Delay(500);
                    }
                }
            }
        }
        private async Task handleConnectionAsync(TcpClient conn, CancellationToken token)
        {
            var handshake = _handshakeAsync(conn);
            string path;
            while (true) {
                if (token.IsCancellationRequested) {
                    return;
                }
                else if (handshake.IsCompleted) {
                    path = await handshake.ConfigureAwait(false);
                    break;
                } else {
                    await Task.Delay(100);
                }
            }

            var client = new Connection(conn);
            if (string.IsNullOrEmpty(path)) {
                client.Kill();
                throw new HandshakeException("Unable to retrieve requested path");
            }

            if (!_servers.ContainsKey(path)) {
                throw new NotImplementedException("Path was not found on the server.");
            }

            _servers[path].RegisterClient(client);
        }
        private async Task<string> _handshakeAsync(TcpClient conn)
        {

            var _stream = conn.GetStream();

            StreamReader reader = new StreamReader(_stream);
            Dictionary<string, string> dict = new Dictionary<string, string>();
            
            string path = string.Empty;

            string line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
            {
                if (line.StartsWith("Sec-WebSocket-Key"))
                    dict.Add("Key", line.Split(':')[1].Trim());
                if (line.StartsWith("GET")) {
                    path = line.Split(' ')[1].Trim();
                    dict.Add("Protocol", line.Split(' ')[1].Trim().Substring(1));   // FIXME: Whats going on here..?
                }
            }

            if (!dict.ContainsKey("Key"))
                throw new HandshakeException("Failed to receive the key necessary to upgrade the connection.");
            string acceptKey = Convert.ToBase64String(
                                    SHA1.Create().ComputeHash(
                                        Encoding.UTF8.GetBytes(
                                            dict["Key"] + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
                                        )
                                    )
                                );

            string response = "HTTP/1.1 101 Switching Protocols" + Environment.NewLine;
            response += "Upgrade: websocket" + Environment.NewLine;
            response += "Connection: Upgrade" + Environment.NewLine;
            response += "Sec-WebSocket-Accept: " + acceptKey + Environment.NewLine;
            if (dict.ContainsKey("Protocol") && !string.IsNullOrEmpty(dict["Protocol"]))
                response += "Sec-WebSocket-Protocol: " + dict["Protocol"] + Environment.NewLine;
            response += Environment.NewLine;
            var bytes = Encoding.UTF8.GetBytes(response);
            await _stream.WriteAsync(bytes, 0, bytes.Length);
            await _stream.FlushAsync();

            return path;
        }
        internal void RegisterServer(WebSocketServer server) {
            lock (_servers)
            {
                _servers.Add(server.Path, server);
            }
        }
        internal void UnregisterServer(WebSocketServer server) {
            lock (_servers)
            {
                _servers.Remove(server.Path);
                if (_servers.Count == 0) {
                    _shutdown();
                }
            }
        }

        private void _shutdown() {
            lock (_servers) {
                DeleteReceiver(Port);
                _tcpListener.Stop();
                cancellationTokenSource.Cancel();
                _connectionListenerTask.Wait();
            }
        }
    }
}
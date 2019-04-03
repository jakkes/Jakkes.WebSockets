using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jakkes.WebSockets;
using Jakkes.WebSockets.Server;
using Xunit;

namespace Jakkes.WebSockets.Test
{
    public class Utilities
    {
        public static readonly Random random = new Random();
        public static WebSocketServer openServer(int port)
            => openServer(port, "/");
        public static WebSocketServer openServer(int port, string path) {
            var re = new WebSocketServer(port, path);
            re.Start();
            Assert.Equal(State.Open, re.State);
            return re;
        }
        public static async Task closeServer(WebSocketServer server) {
            
            if (server.Connections.Any()) {
                server.Close();
                Assert.Equal(State.Closing, server.State);
                
                for (int i = 0; i < 50; i++) {
                    if (server.State == State.Closed)
                        break;
                    await Task.Delay(100);
                }
                Assert.Equal(server.State, State.Closed);
            } else {
                server.Close();
                Assert.Equal(State.Closed, server.State);
            }
        }
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        
        public class byteArrayComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[] x, byte[] y)
            {
                if (x.Length != y.Length)
                    return false;

                for (int i = 0; i < x.Length; i++) {
                    if (x[i] != y[i])
                        return false;
                }

                return true;
            }

            public int GetHashCode(byte[] obj)
            {
                return obj.Sum(x => (int)x);
            }
        }
    }
}
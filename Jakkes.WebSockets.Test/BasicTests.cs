using System;
using System.Threading;
using Xunit;
using System.Collections.Generic;
using Jakkes.WebSockets;
using Jakkes.WebSockets.Server;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;

namespace Jakkes.WebSockets.Test
{
    public class BasicTests
    {
        
        [Fact]
        public async Task StartConnectCloseClientFirst()
        {
            var server = Utilities.openServer(10031);

            var client = await Connection.ConnectAsync("ws://localhost:10031");
            Assert.Equal(client.State, State.Open);

            await Task.Delay(1000);

            client.Close();
            Assert.Equal(client.State, State.Closing);

            for (int i = 0; i < 100; i++) {
                if (client.State == State.Closed)
                    break;
                await Task.Delay(100);
            }
            Assert.Equal(client.State, State.Closed);
            Assert.Equal(server.Connections.Count(), 0);

            await Utilities.closeServer(server);
        }

        [Fact]
        public async Task StartConnectCloseServerFirst()
        {
            var server = Utilities.openServer(8002);

            var client = await Connection.ConnectAsync("ws://localhost:8002");
            Assert.Equal(client.State, State.Open);

            server.Close();
            Assert.Equal(server.State, State.Closing);
            
            for (int i = 0; i < 50; i++) {
                if (server.State == State.Closed)
                    break;
                await Task.Delay(100);
            }
            Assert.Equal(State.Closed, server.State);
            Assert.Equal(State.Closed, client.State);
        }


        [Theory]
        [InlineData(2,10,3,7,31001)]
        [InlineData(1000,1000,1000,1000,31002)]
        [InlineData(3,70000,3,70000,31003)]
        public async Task SendAndReceiveBinary(int serverMessageCount, int serverMessageBytes, int clientMessageCount, int clientMessageBytes, int port) {
            var server = new WebSocketServer(port);

            server.Start();
            Assert.Equal(server.State, State.Open);

            var client = await Connection.ConnectAsync("ws://localhost:" + port);
            Assert.Equal(client.State, State.Open);

            var serverClient = server.Connections.First();

            Dictionary<byte[], bool> serverTexts = new Dictionary<byte[], bool>(serverMessageCount, new Utilities.byteArrayComparer());
            Dictionary<byte[], bool> clientTexts = new Dictionary<byte[], bool>(clientMessageCount, new Utilities.byteArrayComparer());

            for (int i = 0; i < clientMessageCount; i++) {
                var b = new byte[clientMessageBytes];
                Utilities.random.NextBytes(b);
                clientTexts.Add(b, false);
            }

            for (int i = 0; i < serverMessageCount; i++) { 
                var b = new byte[serverMessageBytes];
                Utilities.random.NextBytes(b);
                serverTexts.Add(b, false);
            }

            var lastUpdate = DateTime.Now;

            client.BinaryReceived += (o,e) => {
                Assert.True(serverTexts.ContainsKey(e.Binary));
                
                lock(serverTexts)
                    serverTexts[e.Binary] = true;

                lastUpdate = DateTime.Now;
            };
            serverClient.BinaryReceived += (o,e) => {
                Assert.True(clientTexts.ContainsKey(e.Binary));

                lock(clientTexts)
                    clientTexts[e.Binary] = true;

                lastUpdate = DateTime.Now;
            };

            lock (serverTexts) {
                foreach (var text in serverTexts.Keys) {
                    serverClient.Send(text);
                }
            }

            lock (clientTexts) {
                foreach (var text in clientTexts.Keys) {
                    client.Send(text);
                }
            }

            lastUpdate = DateTime.Now;
            while ((DateTime.Now - lastUpdate).TotalSeconds < 5) {
                await Task.Delay(1000).ConfigureAwait(false);
            }

            Assert.All(clientTexts.Values, (bool x) => Assert.True(x));
            Assert.All(serverTexts.Values, (bool x) => Assert.True(x));

            await Utilities.closeServer(server);
        }

        [Theory]
        [InlineData(100, 1000, 50, 3000, 30001)]
        [InlineData(10, 70000, 10000, 10, 30002)]
        public async Task SendAndReceiveText(int serverMessageCount, int serverMessageBytes, int clientMessageCount, int clientMessageBytes, int port) {
            var server = new WebSocketServer(port);
            
            server.Start();
            Assert.Equal(server.State, State.Open);

            var client = await Connection.ConnectAsync("ws://localhost:" + port);
            Assert.Equal(client.State, State.Open);

            var serverClient = server.Connections.First();

            Dictionary<string, bool> serverTexts = new Dictionary<string, bool>(serverMessageCount);
            Dictionary<string, bool> clientTexts = new Dictionary<string, bool>(clientMessageCount);

            for (int i = 0; i < clientMessageCount; i++) {
                clientTexts.Add(Utilities.RandomString(clientMessageBytes), false);
            }

            for (int i = 0; i < serverMessageCount; i++) { 
                serverTexts.Add(Utilities.RandomString(serverMessageBytes), false);
            }

            var lastUpdate = DateTime.Now;

            client.TextReceived += (o,e) => {
                Assert.True(serverTexts.ContainsKey(e.Text));
                
                lock(serverTexts)
                    serverTexts[e.Text] = true;

                lastUpdate = DateTime.Now;
            };
            serverClient.TextReceived += (o,e) => {
                Assert.True(clientTexts.ContainsKey(e.Text));

                lock(clientTexts)
                    clientTexts[e.Text] = true;

                lastUpdate = DateTime.Now;
            };

            lock (serverTexts) {
                foreach (var text in serverTexts.Keys) {
                    serverClient.Send(text);
                }
            }

            lock (clientTexts) {
                foreach (var text in clientTexts.Keys) {
                    client.Send(text);
                }
            }

            lastUpdate = DateTime.Now;
            while ((DateTime.Now - lastUpdate).TotalSeconds < 5) {
                await Task.Delay(1000).ConfigureAwait(false);
            }

            Assert.All(clientTexts.Values, (bool x) => Assert.True(x));
            Assert.All(serverTexts.Values, (bool x) => Assert.True(x));

            await Utilities.closeServer(server);
        }
    
        [Theory]
        [InlineData(2, 10, 10, 32001)]
        [InlineData(13, 200, 200, 32002)]
        [InlineData(7, 70000, 2, 32003)]
        public async Task BroadcastText(int clients, int bytes, int messages, int port) {
            var server = Utilities.openServer(port);

            var conns = new Connection[clients];

            var msgReceived = new Dictionary<Connection, Dictionary<string, bool>>(clients);
            var msgs = new string[messages];


            for (int i = 0; i < messages; i++) {
                msgs[i] = Utilities.RandomString(bytes);
            }

            var last_update = DateTime.Now;

            for (int i = 0; i < clients; i++) {
                conns[i] = await Connection.ConnectAsync("ws://localhost:" + port);
                msgReceived.Add(conns[i], new Dictionary<string, bool>(messages));

                foreach (var msg in msgs)
                    msgReceived[conns[i]].Add(msg, false);

                conns[i].TextReceived += (o,e) => {
                    msgReceived[(Connection)o][e.Text] = true;
                    last_update = DateTime.Now;
                };
            }

            foreach (var msg in msgs) 
                server.Broadcast(msg);

            await server.FlushAsync();

            last_update = DateTime.Now;
            while ((DateTime.Now - last_update).TotalSeconds < 5) {
                await Task.Delay(1000);
            }

            Assert.All<Dictionary<string, bool>>(msgReceived.Values, (Dictionary<string, bool> dict) => {
                Assert.All<bool>(dict.Values, (bool x) => Assert.True(x));
            });

            await Utilities.closeServer(server);
        }
    }
}

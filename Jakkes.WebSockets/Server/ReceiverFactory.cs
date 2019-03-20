using System.Collections.Generic;

namespace Jakkes.WebSockets.Server
{
    internal sealed partial class Receiver
    {
        private static Dictionary<int, Receiver> _receivers = new Dictionary<int, Receiver>();
        private static void DeleteReceiver(int port) {
            lock (_receivers)
            {
                _receivers.Remove(port);
            }
        }
        internal static Receiver GetReceiver(int port) {
            lock (_receivers)
            {
                if (!_receivers.ContainsKey(port)) {
                    _receivers.Add(port, new Receiver(port));
                }

                return _receivers[port];
            }
        }
    }
}
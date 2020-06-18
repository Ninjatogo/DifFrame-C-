using Difframe;
using SyncronousTaskClient;
using SyncronousTaskServer;

namespace ProcessNode
{
    public class Node
    {
        private NetworkServer _server;
        private NetworkClient _client;
        private ProcessEngine _engine;

        public Node(bool isServer = false)
        {
            if (isServer)
            {
                _server = new NetworkServer();
            }
            else
            {
                _client = new NetworkClient();
                _client.StartClient();
            }
        }
    }
}

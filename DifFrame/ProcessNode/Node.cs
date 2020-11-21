using SyncronousTaskClient;
using SyncronousTaskServer;
using System;

namespace ProcessNode
{
    public class Node
    {
        private bool _isServer;
        private bool _connectionLinkInProgress;
        private bool _connectionEstablished;

        private NetworkServer _server;
        private NetworkClient _client;

        public Node(bool isServer = false)
        {
            _isServer = isServer;
            _connectionLinkInProgress = false;
            _connectionEstablished = false;

            if (_isServer)
            {
                _server = new NetworkServer();
            }
            else
            {
                _client = new NetworkClient();
            }
        }

        // TODO: Start client and server instances using tasks to allow them to be stopped easily and allow node to multitask

        public void StartConnection(string inProjectFolder, double inSimilarityThreshold = 34.50, int inMiniBatchSize = 2, int inPort = 11000)
        {
            EndConnection();

            _server.StartServerListener(inProjectFolder, inSimilarityThreshold, inMiniBatchSize, inPort);
        }

        public bool StartConnection(int inPort = 11000, bool inLocalDataMode = false)
        {
            EndConnection();

            if (_connectionLinkInProgress == false && _connectionEstablished == false)
            {
                _connectionLinkInProgress = true;
                try
                {
                    _client.StartClient(inPort, inLocalDataMode);
                    _connectionEstablished = true;
                }
                catch(Exception e)
                {
                    // Log message
                }
                finally
                {
                    _connectionLinkInProgress = false;
                }
            }
            return false;
        }

        public bool EndConnection()
        {
            if (_isServer)
            {
                _server.StopServer();
            }
            else
            {
                _client.StopClient();
            }
            _connectionEstablished = false;
            return false;
        }
    }
}

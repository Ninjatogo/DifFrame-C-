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

        public Node(bool isServer = false, string inProjectDirectory = null)
        {
            _isServer = isServer;
            _connectionLinkInProgress = false;
            _connectionEstablished = false;

            if (_isServer)
            {
                _server = new NetworkServer(inProjectDirectory);
            }
            else
            {
                _client = new NetworkClient();
            }
        }

        // TODO: Start client and server instances using tasks to allow them to be stopped easily and allow node to multitask

        public bool StartConnection(int inPort = 11000, bool inLocalDataMode = false, double inSimilarityThreshold = 34.50, int inMiniBatchSize = 2)
        {
            EndConnection();

            if (_isServer)
            {
                _server.StartServerListener(inSimilarityThreshold, inMiniBatchSize, inPort);
            }
            else
            {
                if (_connectionLinkInProgress == false && _connectionEstablished == false)
                {
                    _connectionLinkInProgress = true;
                    try
                    {
                        _client.StartClient(inPort, inLocalDataMode);
                        _connectionEstablished = true;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                    finally
                    {
                        _connectionLinkInProgress = false;
                    }
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

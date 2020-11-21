using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Difframe;
using LiteDB.Engine;
using NetworkDataTools;

namespace SyncronousTaskServer
{
    public class NetworkServer
    {
        private double _similarityThreshold;
        private int _miniBatchSize;
        private bool _endConnectionSignalReceived;
        private ProcessEngine _engine;
        private static Stack<int> testRange = new Stack<int>();
        private IPAddress _ipAddress;

        public NetworkServer()
        {
            _endConnectionSignalReceived = false;
            _engine = new ProcessEngine(true, null, _similarityThreshold, _miniBatchSize);
        }

        
        private static int[] GetNextFrameRange(int inMaxLength = 10)
        {
            var currentRange = new List<int>();

            // Return next set of frames to be processed
            if(testRange.Count > 0)
            {
                if(testRange.Count >= inMaxLength)
                {
                    for(var i = 0; i < inMaxLength; i++)
                    {
                        currentRange.Add(testRange.Pop());
                    }
                }
                else
                {
                    currentRange.Add(testRange.Pop());
                }
            }

            return currentRange.ToArray();
        }

        private static void SaveFrameBlocks(List<int[]> inFrameBlocks)
        {
            foreach (var arr in inFrameBlocks)
            {
                Console.WriteLine("Array contents: ");
                foreach (var num in arr)
                {
                    Console.Write($"{num}, ");
                }
                Console.WriteLine();
            }
        }

        private string ClientInitiation(Socket inHandler, string inFileName)
        {
            string clientName = null;

            // Handshake Loop - Abort if loop fails 10 times
            for(var i = 0; i < 10; i++)
            {
                var failureDetected = false;

                // Send video filename to client
                NT.SendString(inHandler, inFileName);
                // Receive filename echo from client
                var data = NT.ReceiveString(inHandler);
                // If client doesn't echo filename restart handshaking loop 
                if (data != inFileName)
                {
                    failureDetected = true;
                }

                // Send similarity threshold to client
                NT.SendDouble(inHandler, _similarityThreshold);
                // Receive threshold echo from client
                var echoThreshold = NT.ReceiveDouble(inHandler);
                if (echoThreshold != _similarityThreshold)
                {
                    failureDetected = true;
                }

                // Send mini batch size to client
                NT.SendInt(inHandler, _miniBatchSize);
                // Receive batch size echo from client
                var echoBatchSize = NT.ReceiveInt(inHandler);
                if (echoBatchSize != _miniBatchSize)
                {
                    failureDetected = true;
                }

                // If returned checksum matches what server has calculated
                if (failureDetected == false)
                {
                    // Send file good confirmation to client
                    NT.SendString(inHandler, "OK");

                    // Receive client name
                    data = NT.ReceiveString(inHandler);

                    // Proceed to connection handler stage 2
                    clientName = data;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Client completed initiation handshake.");
                    Console.ResetColor();
                    break;
                }
                else
                {
                    // Send fail message to client
                    NT.SendString(inHandler, "FAIL");
                    continue;
                }
            }

            return clientName;
        }

        private void IssueClientFrameProcessRequests(Socket inHandler)
        {
            while (true)
            {
                var nextFrameRange = GetNextFrameRange();

                // If there are no more frames to process, exit loop allow connections to be closed
                if (nextFrameRange.Length == 0)
                {
                    // Send frame indices to be processed
                    NT.SendInt(inHandler, 0);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Frames left to process: {nextFrameRange.Length}");
                    Console.ResetColor();
                    break;
                }
                NT.SendIntCollections(inHandler, nextFrameRange);

                // Receive processed frame data
                var processOutput = NT.ReceiveIntCollections(inHandler);
                while (processOutput.receivedSuccessfully)
                {
                    // Save process output to database
                    SaveFrameBlocks(processOutput.collections);

                    processOutput = NT.ReceiveIntCollections(inHandler);
                }

            }
        }

        private void UploadFrames(int[] inFrames)
        {
            //
        }

        /// <summary>
        /// Listen for wandering clients looking for a server to connect to. Guide them home.
        /// </summary>
        /// <returns></returns>
        private Task GuideNewClients(int inListenPort = 11500)
        {
            return Task.Run(() =>
            {
                var Server = new UdpClient(inListenPort);
                try
                {
                    var ResponseData = Encoding.ASCII.GetBytes("Difframe Node:Server");

                    while (true)
                    {
                        var ClientEp = new IPEndPoint(IPAddress.Any, 0);
                        var ClientRequestData = Server.Receive(ref ClientEp);
                        var ClientRequest = Encoding.ASCII.GetString(ClientRequestData);

                        if(ClientRequest == "Difframe Node:Client")
                        {
                            Console.WriteLine($"Recived {ClientRequest} from {ClientEp.Address}, sending response");
                            Server.Send(ResponseData, ResponseData.Length, ClientEp);
                        }
                    }
                }
                catch(Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e);
                    Console.ResetColor();
                }
                finally
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Network autodiscovery client guide shutting down.");
                    Console.ResetColor();
                    Server.Close();
                }
            });
        }

        private Task HandleFileDownloadRequests(Socket inHandler)
        {
            return Task.Run(() =>
            {
                try
                {
                    // Receive frame requests
                    var frameRequests = NT.ReceiveIntCollections(inHandler);

                    // Send frame data
                    if (frameRequests.receivedSuccessfully)
                    {
                        //
                    }
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e);
                    Console.ResetColor();
                }
                finally
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Socket shutdown!");
                    inHandler.Shutdown(SocketShutdown.Both);
                    inHandler.Close();
                    Console.ResetColor();
                }
            });
        }

        private Task AwaitFileDownloadRequests(int inListenPort = 11501)
        {
            return Task.Run(() =>
            {
                var localEndPoint = new IPEndPoint(_ipAddress, inListenPort);
                // Create a TCP/IP socket.
                using (var listener = new Socket(_ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp))
                {
                    // Bind the socket to the local endpoint and
                    // listen for incoming connections.  
                    try
                    {
                        listener.Bind(localEndPoint);
                        listener.Listen(10);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"TCP server listening on {listener.LocalEndPoint}");
                        Console.ResetColor();
                        // Start listening for connections.  
                        while (_endConnectionSignalReceived == false)
                        {
                            Console.WriteLine("Waiting for a connection...");
                            // Program is suspended while waiting for an incoming connection.
                            var handler = listener.Accept();
                            Console.WriteLine("Connection received and being handled");
                            HandleFileDownloadRequests(handler);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(e.ToString());
                        Console.ResetColor();
                    }
                }
            });
        }

        private Task HandleNewClientNode(Socket inHandler, string inFileName)
        {
            return Task.Run(() =>
            {
                try
                {
                    var clientName = ClientInitiation(inHandler, inFileName);
                    if(clientName != null)
                    {
                        IssueClientFrameProcessRequests(inHandler);
                    }
                }
                catch(Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e);
                    Console.ResetColor();
                }
                finally
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Socket shutdown!");
                    inHandler.Shutdown(SocketShutdown.Both);
                    inHandler.Close();
                    Console.ResetColor();
                }
            });
        }

        public void StartServerListener(string inProjectFolder, double inSimilarityThreshold = 34.50, int inMiniBatchSize = 2, int inPort = 11000)
        {
            _similarityThreshold = inSimilarityThreshold;
            _miniBatchSize = inMiniBatchSize;
            _engine.UpdateProjectInput(inProjectFolder);

            for (int i = 0; i < _engine.GetLastFrameIndex(); i++)
            {
                testRange.Push(i);
            }

            // Establish the local endpoint for the socket.  
            // Dns.GetHostName returns the name of the
            // host running the application.  
            var ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            var ipAddress = ipHostInfo.AddressList[1];
            if (ipHostInfo.AddressList.Length > 1)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Multiple host IPs found!");
                Console.ResetColor();
                while(true)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("Please choose the IP you would like to use for TCP server:");
                    for (var i = 0; i < ipHostInfo.AddressList.Length; i++)
                    {
                        Console.WriteLine($"({i}) - {ipHostInfo.AddressList[i]}");
                    }
                    Console.ResetColor();
                    var choice = Console.ReadLine();
                    if (int.TryParse(choice, out int choiceInt))
                    {
                        if ((choiceInt >= 0) && (choiceInt < ipHostInfo.AddressList.Length))
                        {
                            ipAddress = ipHostInfo.AddressList[choiceInt];
                            break;
                        }
                    }
                }
            }

            _ipAddress = ipAddress;
            var localEndPoint = new IPEndPoint(ipAddress, inPort);

            // Create UDP client guide.
            GuideNewClients();
            // Create file download request handler.
            AwaitFileDownloadRequests();

            // Create a TCP/IP socket.  
            using (var listener = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp))
            {

                // Bind the socket to the local endpoint and
                // listen for incoming connections.  
                try
                {
                    listener.Bind(localEndPoint);
                    listener.Listen(10);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"TCP server listening on {listener.LocalEndPoint}");
                    Console.ResetColor();
                    // Start listening for connections.  
                    while (_endConnectionSignalReceived == false)
                    {
                        Console.WriteLine("Waiting for a connection...");
                        // Program is suspended while waiting for an incoming connection.
                        var handler = listener.Accept();
                        Console.WriteLine("Connection received and being handled");
                        HandleNewClientNode(handler, "file name test");
                    }
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e.ToString());
                    Console.ResetColor();
                }
            }
            Console.WriteLine("\nPress ENTER to continue...");
            Console.ReadLine();
        }

        public void StopServer()
        {
            //
        }
    }
}
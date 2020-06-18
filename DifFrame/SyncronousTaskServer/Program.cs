using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Difframe;
using NetworkDataTools;

namespace SyncronousTaskServer
{
    public class NetworkServer
    {
        private double _similarityThreshold;
        private int _miniBatchSize;
        private ProcessEngine _engine;

        public NetworkServer(double inSimilarityThreshold, int inMiniBatchSize)
        {
            _similarityThreshold = inSimilarityThreshold;
            _miniBatchSize = inMiniBatchSize;
            _engine = new ProcessEngine(_similarityThreshold, null, _miniBatchSize);
        }

        private static Stack<int> testRange = new Stack<int>();
        
        private static int[] GetNextFrameRange(int inMaxLength = 10)
        {
            var currentRange = new List<int>();

            // Return next set of frames to be processed
            if(testRange.Count > 0)
            {
                currentRange.Add(testRange.Pop());
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

        private string ClientInitiation(Socket inHandler, string inFileName, string inFileLocation, string inChecksum)
        {
            string clientName = null;

            // Handshake Loop - Abort if loop fails 10 times
            for(var i = 0; i < 10; i++)
            {
                // Send video filename to client
                NT.SendString(inHandler, inFileName);

                // Receive filename echo from client
                var data = NT.ReceiveString(inHandler);

                // If client doesn't echo filename restart handshaking loop 
                if (data != inFileName)
                {
                    // Send fail message to client
                    NT.SendString(inHandler, "FAIL");
                    continue;
                }

                // Send video file location to client
                NT.SendString(inHandler, inFileLocation);

                // Receive file location echo from client
                data = NT.ReceiveString(inHandler);

                // Send similarity threshold to client
                NT.SendDouble(inHandler, _similarityThreshold);

                // Receive threshold echo from client
                var echoThreshold = NT.ReceiveDouble(inHandler);

                // Send mini batch size to client
                NT.SendInt(inHandler, _miniBatchSize);

                // Receive batch size echo from client
                var echoBatchSize = NT.ReceiveInt(inHandler);


                // Receive checksum response from client
                data = NT.ReceiveString(inHandler);

                // If returned checksum matches what server has calculated
                if (data == inChecksum)
                {
                    // Send file good confirmation to client
                    NT.SendString(inHandler, "OK");

                    // Receive client name
                    data = NT.ReceiveString(inHandler);

                    if (data != "FAIL")
                    {
                        // Proceed to connection handler stage 2
                        clientName = data;
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Client completed initiation handshake.");
                        Console.ResetColor();
                        break;
                    }
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

                // Save process output to database
                SaveFrameBlocks(processOutput.collections);
            }
        }

        public Task HandleNewConnection(Socket inHandler, string inFileName, string inFileLocation, string inChecksum)
        {
            return Task.Run(() =>
            {
                try
                {
                    var clientName = ClientInitiation(inHandler, inFileName, inFileLocation, inChecksum);
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

        public void StartServerListener(string inProjectFolder)
        {
            _engine.UpdateProjectInput(inProjectFolder);

            // Establish the local endpoint for the socket.  
            // Dns.GetHostName returns the name of the
            // host running the application.  
            var ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            var ipAddress = ipHostInfo.AddressList[0];
            var localEndPoint = new IPEndPoint(ipAddress, 11000);

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

                    // Start listening for connections.  
                    while (true)
                    {
                        Console.WriteLine("Waiting for a connection...");
                        // Program is suspended while waiting for an incoming connection.  
                        var handler = listener.Accept();
                        Console.WriteLine("Connection received and being handled");
                        HandleNewConnection(handler, "file name test", "file location test", "file checksum test");
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
            Console.WriteLine("\nPress ENTER to continue...");
            Console.ReadLine();
        }
    }
}
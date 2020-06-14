using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NetworkDataTools;

namespace SyncronousTaskServer
{
    public class SynchronousSocketListener
    {
        private static Stack<int> testRange = new Stack<int>();
        private static string GetChecksum(string inFileLocation)
        {
            using (var stream = new BufferedStream(File.OpenRead(inFileLocation), 1200000))
            {
                SHA256Managed sha = new SHA256Managed();
                byte[] checksum = sha.ComputeHash(stream);
                return BitConverter.ToString(checksum).Replace("-", String.Empty);
            }
        }

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

        private static string ClientInitiation(Socket inHandler, string inFileName, string inFileLocation, string inChecksum)
        {
            string clientName = null;

            // Handshake Loop - Abort if loop fails 10 times
            for(var i = 0; i < 10; i++)
            {
                // Byte buffer for received client messages
                var bytes = new byte[4096];

                // String buffer for received client messages
                string data = null;

                // Byte array for converted messages to send to client
                var msg = Encoding.ASCII.GetBytes(inFileName);

                // Send video filename to client
                inHandler.Send(msg);

                // Receive response from client
                var bytesRec = inHandler.Receive(bytes);
                data = Encoding.ASCII.GetString(bytes, 0, bytesRec);

                // If client doesn't echo filename restart handshaking loop 
                if(data != inFileName)
                {
                    // Send fail message to client
                    msg = Encoding.ASCII.GetBytes("FAIL");
                    inHandler.Send(msg);
                    continue;
                }

                // Send video file location to client
                msg = Encoding.ASCII.GetBytes(inFileLocation);
                inHandler.Send(msg);

                // Receive checksum response from client
                bytesRec = inHandler.Receive(bytes);
                data = Encoding.ASCII.GetString(bytes, 0, bytesRec);

                // If returned checksum matches what server has calculated
                if(data == inChecksum)
                {
                    // Send file good confirmation to client
                    msg = Encoding.ASCII.GetBytes("OK");
                    inHandler.Send(msg);

                    // Receive client name
                    bytesRec = inHandler.Receive(bytes);
                    data = Encoding.ASCII.GetString(bytes, 0, bytesRec);

                    if(data != "FAIL")
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
                    msg = Encoding.ASCII.GetBytes("FAIL");
                    inHandler.Send(msg);
                    continue;
                }
            }

            return clientName;
        }

        private static void IssueClientFrameProcessRequests(Socket inHandler)
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

        public static Task HandleNewConnection(Socket inHandler, string inFileName, string inFileLocation, string inChecksum)
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

        public static void StartServerListener()
        {
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

        public static int Main(string[] args)
        {
            testRange.Push(1);
            testRange.Push(2);
            testRange.Push(3);
            testRange.Push(4);
            testRange.Push(1);
            testRange.Push(2);
            testRange.Push(3);
            testRange.Push(4);
            testRange.Push(1);
            testRange.Push(2);
            testRange.Push(3);
            testRange.Push(4);
            testRange.Push(1);
            testRange.Push(2);
            testRange.Push(3);
            testRange.Push(4);
            testRange.Push(1);
            testRange.Push(2);
            testRange.Push(3);
            testRange.Push(4);
            testRange.Push(1);
            testRange.Push(2);
            testRange.Push(3);
            testRange.Push(4);
            testRange.Push(1);
            testRange.Push(2);
            testRange.Push(3);
            testRange.Push(4);
            testRange.Push(1);
            testRange.Push(2);
            testRange.Push(3);
            testRange.Push(4);
            testRange.Push(1);
            testRange.Push(2);
            testRange.Push(3);
            testRange.Push(4);
            testRange.Push(1);
            testRange.Push(2);
            testRange.Push(3);
            testRange.Push(4);
            Console.WriteLine("Starting task-based server...");
            StartServerListener();
            return 0;
        }
    }
}
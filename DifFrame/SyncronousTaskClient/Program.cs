using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetworkDataTools;

namespace SyncronousTaskClient
{
    class SyncronousTaskClient
    {
        public static Task HandleNewConnection(Socket inHandler)
        {
            return Task.Run(() =>
            {
                // Data buffer for incoming data.  
                byte[] bytes = new byte[1200];

                // Incoming data from the client.
                string data = null;

                // An incoming connection needs to be processed.  
                while (true)
                {
                    int bytesRec = inHandler.Receive(bytes);
                    data += Encoding.ASCII.GetString(bytes, 0, bytesRec);
                    if (data.IndexOf("<EOF>") > -1)
                    {
                        break;
                    }
                }

                // Show the data on the console.  
                Console.WriteLine("Text received : {0}", data);

                // Echo the data back to the client.  
                byte[] msg = Encoding.ASCII.GetBytes(data);

                inHandler.Send(msg);
                inHandler.Shutdown(SocketShutdown.Both);
                inHandler.Close();
            });
        }

        private static (bool sucessfulInitiaition, string fileName, string fileLocation) ServerInitiation(Socket inHandler)
        {
            string _fileName = null;
            string _fileLocation = null;
            var initiationSuccessful = false;

            // Handshake Loop - Abort if loop fails 10 times
            for (var i = 0; i < 10; i++)
            {
                // Byte buffer for received server messages
                var bytes = new byte[4096];

                // Receive filename from server
                var bytesRec = inHandler.Receive(bytes);
                _fileName = Encoding.ASCII.GetString(bytes, 0, bytesRec);

                // Byte array for converted messages to send to server
                var msg = Encoding.ASCII.GetBytes(_fileName);

                // Echo video filename to server
                inHandler.Send(msg);

                // Receive file location from server
                bytesRec = inHandler.Receive(bytes);
                _fileLocation = Encoding.ASCII.GetString(bytes, 0, bytesRec);

                // Send file checksum to ensure both machines are referring to same file
                msg = Encoding.ASCII.GetBytes("file checksum test");
                inHandler.Send(msg);

                // Receive "file good" confirmation from server
                bytesRec = inHandler.Receive(bytes);
                var response = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                if(response == "OK")
                {
                    // Send machine name to server
                    msg = Encoding.ASCII.GetBytes("client_name");
                    inHandler.Send(msg);

                    initiationSuccessful = true;
                    break;
                }
            }

            // Proceed to connection handler stage 2
            return (initiationSuccessful, _fileName, _fileLocation);
        }

        private static void ReceiveFrameProcessRequests(Socket inHandler)
        {
            while (true)
            {
                var frameRangeToProcess = NT.ReceiveIntCollections(inHandler);
                foreach (var arr in frameRangeToProcess.collections)
                {
                    Console.WriteLine("Array contents: ");
                    foreach (var num in arr)
                    {
                        Console.Write($"{num}, ");
                    }
                    Console.WriteLine();
                }

                if (frameRangeToProcess.receivedSuccessfully == false)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Frame range to process count: {frameRangeToProcess.collections.Count}");
                    Console.ResetColor();
                    break;
                }

                // Process data for frame range
                var processedData = new List<int>() { 11, 23, 36 };

                NT.SendIntCollections(inHandler, processedData.ToArray());
            }
        }

        public static void StartClient()
        {
            try
            {
                // Establish the remote endpoint for the socket.
                var ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                var ipAddress = ipHostInfo.AddressList[0];
                var remoteEP = new IPEndPoint(ipAddress, 11000);

                // Create a TCP/IP  socket.  
                using var sender = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    sender.Connect(remoteEP);

                    var resultsTuple = ServerInitiation(sender);
                    ReceiveFrameProcessRequests(sender);
                }
                catch (ArgumentNullException ane)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("ArgumentNullException : {0}", ane.ToString());
                    Console.ResetColor();
                }
                catch (SocketException se)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("SocketException : {0}", se.ToString());
                    Console.ResetColor();
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Unexpected exception : {0}", e.ToString());
                    Console.ResetColor();
                }
                finally
                {
                    // Release the socket.
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Socket shutdown!");
                    sender.Shutdown(SocketShutdown.Both);
                    sender.Close();
                    Console.ResetColor();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        static void Main(string[] args)
        {
            StartClient();
            Console.ReadLine();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Difframe;
using NetworkDataTools;

namespace SyncronousTaskClient
{
    public class NetworkClient
    {
        private ProcessEngine _engine;

        public NetworkClient()
        {
            //
        }

        private (bool sucessfulInitiaition, double similarityThreshold, int miniBatchSize, string fileLocation) ServerInitiation(Socket inHandler)
        {
            string _fileLocation = null;
            var _initiationSuccessful = false;
            double _similarityThreshold = 0.0;
            int _miniBatchSize = 0;

            // Handshake Loop - Abort if loop fails 10 times
            for (var i = 0; i < 10; i++)
            {
                // Receive filename from server
                var _fileName = NT.ReceiveString(inHandler);

                // Echo video filename to server
                NT.SendString(inHandler, _fileName);

                // Receive file location from server
                _fileLocation = NT.ReceiveString(inHandler);
                if(_fileLocation == "FAIL")
                {
                    continue;
                }

                // Echo file location
                NT.SendString(inHandler, _fileLocation);

                // Send file checksum to ensure both machines are referring to same file
                NT.SendString(inHandler, "file checksum test");

                // Receive "file good" confirmation from server
                var response = NT.ReceiveString(inHandler);
                if (response == "OK")
                {
                    // Send machine name to server
                    NT.SendString(inHandler, "client_name");

                    _initiationSuccessful = true;
                    break;
                }
            }

            // Proceed to connection handler stage 2
            return (_initiationSuccessful, _similarityThreshold, _miniBatchSize, _fileLocation);
        }

        private void ReceiveFrameProcessRequests(Socket inHandler)
        {
            while (true)
            {
                var frameRangeToProcess = NT.ReceiveIntCollections(inHandler);
                if (frameRangeToProcess.receivedSuccessfully)
                {
                    foreach(var arr in frameRangeToProcess.collections)
                    {
                        _engine.IdentifyDifferences(arr);
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Frame range to process count: {frameRangeToProcess.collections.Count}");
                    Console.ResetColor();
                    break;
                }

                // Process data for frame range
                var processedData = _engine.GetDifferenceBlocks();

                NT.SendIntCollections(inHandler, processedData);
            }
        }

        public void StartClient(IPAddress inIpAddress, int inPort = 11000)
        {
            // Abort if loop fails 10 times
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    var remoteEP = new IPEndPoint(inIpAddress, inPort);

                    // Create a TCP/IP  socket.  
                    using var sender = new Socket(inIpAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        sender.Connect(remoteEP);

                        var resultsTuple = ServerInitiation(sender);

                        if (resultsTuple.sucessfulInitiaition)
                        {
                            _engine = new ProcessEngine();
                            // TODO: Update server initiation to pass back similarity threshold, mini batch size and dice rate
                            _engine.SyncEngineSettings();
                            _engine.UpdateEngineProjectFolder(resultsTuple.fileLocation);

                            ReceiveFrameProcessRequests(sender);
                        }
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
        }
    }
}

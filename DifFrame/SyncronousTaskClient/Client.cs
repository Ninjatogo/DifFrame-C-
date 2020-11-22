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
        private IPEndPoint _serverIpEndPoint;

        public NetworkClient()
        {
            //
        }

        private (bool sucessfulInitiaition, double similarityThreshold, int miniBatchSize) ServerInitiation(Socket inHandler)
        {
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

                // Receive similarity threshold from server
                _similarityThreshold = NT.ReceiveDouble(inHandler);
                // Echo similarity threshold
                NT.SendDouble(inHandler, _similarityThreshold);

                // Receive mini batch size from server
                _miniBatchSize = NT.ReceiveInt(inHandler);
                // Echo mini batch size
                NT.SendInt(inHandler, _miniBatchSize);

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
            return (_initiationSuccessful, _similarityThreshold, _miniBatchSize);
        }

        private void ReceiveFrameProcessRequests(Socket inHandler, bool inLocalDataMode)
        {
            while (true)
            {
                var frameRangeToProcess = NT.ReceiveIntCollections(inHandler);
                if (frameRangeToProcess.receivedSuccessfully)
                {
                    foreach(var arr in frameRangeToProcess.collections)
                    {
                        if (inLocalDataMode == false)
                        {
                            if (DownloadFrames(arr))
                            {
                                Console.WriteLine("Frame download successful!");
                            }
                        }

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Frame range to process: {arr[0]}-{arr[^1]}");
                        Console.ResetColor();

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
                while(processedData.Length > 0)
                {
                    // Send processed results back to server
                    NT.SendIntCollections(inHandler, processedData);

                    processedData = _engine.GetDifferenceBlocks();
                }
            }
        }

        public IPEndPoint FindServer(int inBroadcastPort = 11500)
        {
            var Client = new UdpClient();
            Client.Client.ReceiveTimeout = 2000;
            Client.Client.SendTimeout = 500;

            var ServerEp = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                var RequestData = Encoding.ASCII.GetBytes("Difframe Node:Client");

                Client.EnableBroadcast = true;

                while (true)
                {
                    Client.Send(RequestData, RequestData.Length, new IPEndPoint(IPAddress.Broadcast, inBroadcastPort));

                    var ServerResponseData = Client.Receive(ref ServerEp);
                    var ServerResponse = Encoding.ASCII.GetString(ServerResponseData);
                    if (ServerResponse == "Difframe Node:Server")
                    {
                        Console.WriteLine($"Recived {ServerResponse} from {ServerEp.Address}");
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                Client.Close();
            }
            return ServerEp;
        }

        private bool DownloadFrames(int[] inFrames, int inPort = 11501)
        {
            var downloadSuccessful = false;
            var remoteEP = new IPEndPoint(_serverIpEndPoint.Address, inPort);
            using var sender = new Socket(_serverIpEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                sender.Connect(remoteEP);

                var frameDatas = new Dictionary<int, byte[]>();

                // Notify server of how many requests to expect
                NT.SendInt(sender, inFrames.Length);

                // For each frame request, send frame index then download frame from server
                foreach(var frameIndex in inFrames)
                {
                    // Send frame request
                    NT.SendInt(sender, frameIndex);

                    // Receive frame data
                    var frameData = NT.ReceiveByteArray(sender);

                    // Store frame data in temporary dictionary
                    frameDatas[frameIndex] = frameData;
                }

                _engine.LoadDownloadedFrameData(frameDatas);

                downloadSuccessful = true;
            }
            catch (ArgumentNullException ane)
            {
                downloadSuccessful = false;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ArgumentNullException : {ane}");
                Console.ResetColor();
            }
            catch (SocketException se)
            {
                downloadSuccessful = false;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"SocketException : {se}");
                Console.ResetColor();
            }
            catch (Exception e)
            {
                downloadSuccessful = false;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Unexpected exception : {e}");
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

            return downloadSuccessful;
        }

        public void StartClient(int inPort = 11000, bool inLocalDataMode = false)
        {
            // Abort if loop fails 10 times
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    var serverIp = FindServer();
                    _serverIpEndPoint = serverIp;
                    var remoteEP = new IPEndPoint(_serverIpEndPoint.Address, inPort);

                    // Create a TCP/IP  socket.  
                    using var sender = new Socket(_serverIpEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        sender.Connect(remoteEP);

                        var resultsTuple = ServerInitiation(sender);

                        if (resultsTuple.sucessfulInitiaition)
                        {
                            Console.WriteLine("Initiated with server successfully.");
                            _engine = new ProcessEngine(inLocalDataMode, null, resultsTuple.similarityThreshold, resultsTuple.miniBatchSize);

                            ReceiveFrameProcessRequests(sender, inLocalDataMode);
                        }
                    }
                    catch (ArgumentNullException ane)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"ArgumentNullException : {ane}");
                        Console.ResetColor();
                    }
                    catch (SocketException se)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"SocketException : {se}");
                        Console.ResetColor();
                    }
                    catch (Exception e)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Unexpected exception : {e}");
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

        public void StopClient()
        {
            //
        }
    }
}

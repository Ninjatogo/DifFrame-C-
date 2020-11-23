using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace NetworkDataTools
{
    public static class NT
    {
        public static void SendByteArray(Socket inHandler, byte[] inBytes)
        {
            // Send byte count to expect
            SendInt(inHandler, inBytes.Length);

            var response = ReceiveString(inHandler);
            if(response != "Ready")
            {
                throw new Exception("Client ready resonse not received correctly");
            }

            inHandler.Send(inBytes);

            // Receive client finished download confirmation
            response = ReceiveString(inHandler);
            if (response != "Ok")
            {
                throw new Exception("Client finished resonse not received correctly");
            }
        }

        public static void SendInt(Socket inHandler, int inInt)
        {
            inHandler.Send(DT.ConvertIntToByteArray(inInt));
        }

        public static void SendDouble(Socket inHandler, double inDouble)
        {
            SendString(inHandler, DT.ConvertDoubleToString(inDouble));
        }

        public static void SendString(Socket inHandler, string inString)
        {
            // Byte array for converted messages to send to server
            var msg = Encoding.ASCII.GetBytes(inString);

            // Send message to server
            inHandler.Send(msg);
        }

        /// <summary>
        /// Convert int array to binary arrays, then notify other end of connection of how many arrays to expect and send the arrays.
        /// Requires confirmation echo from client before data is sent.
        /// </summary>
        /// <param name="inHandler"></param>
        /// <param name="inArray"></param>
        public static (bool sentSuccessfully, Exception innerException) SendIntCollections(Socket inHandler, int[] inArray)
        {
            Exception defaultException = new Exception("No issues occurred.");

            try
            {
                var serializedIntCollection = DT.SerializeIntCollection(inArray);

                try
                {
                    SendInt(inHandler, serializedIntCollection.collection.Count);
                }
                catch (Exception e)
                {
                    return (false, e);
                }

                int clientEcho;
                try
                {
                    clientEcho = ReceiveInt(inHandler);
                }
                catch (Exception e)
                {
                    return (false, e);
                }

                if (clientEcho != serializedIntCollection.collection.Count)
                {
                    defaultException = new Exception("Client not echoing the same count for collection count, end connection");
                    return (false, defaultException);
                }

                foreach (var byteArray in serializedIntCollection.collection)
                {
                    try
                    {
                        inHandler.Send(byteArray);
                    }
                    catch (Exception e)
                    {
                        return (false, e);
                    }
                }
            }
            catch(Exception d)
            {
                return (false, d);
            }
            

            return (true, defaultException);
        }

        public static byte[] ReceiveByteArray(Socket inHandler)
        {
            // Container for buffer collections
            var buffer = new List<byte>();
            var receivedBytes = 0;

            // Receive expected byte count
            var expectedCount = ReceiveInt(inHandler);

            // Signal to client that ready to recieve
            SendString(inHandler, "Ready");

            while (receivedBytes < expectedCount)
            {
                // Byte buffer for received server messages
                var bytes = new byte[32768];

                // Receive message from server
                var bytesRec = inHandler.Receive(bytes);

                // If byte buffer fills, simply add to list
                if (bytesRec == bytes.Length)
                {
                    buffer.AddRange(bytes);
                }
                // Else only add the received bytes to list
                else
                {
                    for (int i = 0; i < bytesRec; i++)
                    {
                        buffer.Add(bytes[i]);
                    }
                }

                receivedBytes += bytesRec;
            }

            // Send finished confirmation
            SendString(inHandler, "Ok");

            return buffer.ToArray();
        }

        public static int ReceiveInt(Socket inHandler)
        {
            // Data buffer for incoming data.  
            var bytes = new byte[12];
            byte[] bytesTrimmed;

            // Receive number of arrays to expect for next payload
            int bytesRec = inHandler.Receive(bytes);
            bytesTrimmed = new byte[bytesRec];
            Array.Copy(bytes, bytesTrimmed, bytesRec);
            var numberOfArraysToExpect = DT.ConvertByteArrayToInt(bytesTrimmed);

            return numberOfArraysToExpect;
        }

        public static double ReceiveDouble(Socket inHandler)
        {
            var number = ReceiveString(inHandler);
            return DT.ConvertStringToDouble(number);
        }

        public static string ReceiveString(Socket inHandler)
        {
            // Byte buffer for received server messages
            var bytes = new byte[4096];

            // Receive message from server
            var bytesRec = inHandler.Receive(bytes);
            var message = Encoding.ASCII.GetString(bytes, 0, bytesRec);

            return message;
        }

        /// <summary>
        /// Receive number of collections (x) to expect from other end of connection, then receive x number of byte arrays which are then converted to int array.
        /// </summary>
        /// <param name="inHandler"></param>
        /// <returns></returns>
        public static (bool receivedSuccessfully, List<int[]> collections, Exception innerException) ReceiveIntCollections(Socket inHandler)
        {
            var processOutput = new List<int[]>();
            Exception defaultException = new Exception("No issues occurred.");

            // Data buffer for incoming data.  
            byte[] bytes;
            int bytesRec;
            byte[] bytesTrimmed;
            int numberofArraysToExpect;
            try
            {
                numberofArraysToExpect = ReceiveInt(inHandler);

                if (numberofArraysToExpect == 0)
                {
                    defaultException = new Exception("Client indicated that there are no ints to receive.");
                    return (false, processOutput, defaultException);
                }

                // Echo number back to server
                SendInt(inHandler, numberofArraysToExpect);
            }
            catch(Exception d)
            {
                return (false, processOutput, d);
            }

            try
            {
                for (var i = 0; i < numberofArraysToExpect; i++)
                {
                    bytes = new byte[1200];
                    bytesRec = inHandler.Receive(bytes);
                    bytesTrimmed = new byte[bytesRec];
                    Array.Copy(bytes, bytesTrimmed, bytesRec);
                    var deserialized = DT.DeserializeIntArray(bytesTrimmed);
                    processOutput.Add(deserialized);
                }
            }
            catch (Exception d)
            {
                return (false, processOutput, d);
            }
            

            return (true, processOutput, defaultException);
        }
    }

    public static class DT
    {
        private static List<List<int>> SplitList(List<int> inList, int chunkSize = 10)
        {
            var workingList = new List<List<int>>();

            for(var i = 0; i < inList.Count; i += chunkSize)
            {
                workingList.Add(inList.GetRange(i, Math.Min(chunkSize, inList.Count - i)));
            }

            return workingList;
        }

        private static (List<byte[]> collection, int byteCount) SerializeIntArray(int[] inCollection)
        {
            // Limit each byte[] to 61 ints (first int indicates collection length)
            // Use multiple of 3 for frame number, X, Y
            var bytesCollections = new List<byte[]>();
            var byteCount = 0;

            var choppedInput = SplitList(inCollection.ToList(), 60);

            foreach(var chopList in choppedInput)
            {
                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write(chopList.Count);
                    foreach (int i in chopList)
                    {
                        writer.Write(i);
                    }
                    writer.Close();
                    var arr = ms.ToArray();
                    byteCount += arr.Length;
                    bytesCollections.Add(arr);
                }
            }
            
            return (bytesCollections, byteCount);
        }

        public static (List<byte[]> collection, int byteCount) SerializeIntCollection(int[] inCollection)
        {
            return SerializeIntArray(inCollection);
        }

        public static (List<byte[]> collection, int byteCount) SerializeIntCollection(List<int> inCollection)
        {
            return SerializeIntArray(inCollection.ToArray());
        }

        public static int[] DeserializeIntArray(byte[] inCollection)
        {
            var currentCollection = new List<int>();

            using (var ms = new MemoryStream(inCollection))
            using (var reader = new BinaryReader(ms))
            {
                var count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    currentCollection.Add(reader.ReadInt32());
                }
            }

            return currentCollection.ToArray();
        }

        /// <summary>
        /// Use when sending ints over the network. Converts int to big endian format and then to byte array.
        /// </summary>
        /// <param name="inInt">Input integer can be big/little-endian</param>
        /// <returns></returns>
        public static byte[] ConvertIntToByteArray(int inInt)
        {
            var convertedInt1 = IPAddress.HostToNetworkOrder(inInt);
            return BitConverter.GetBytes(convertedInt1);
        }

        public static string ConvertDoubleToString(double inDouble)
        {
            var invC = CultureInfo.InvariantCulture.NumberFormat;
            return inDouble.ToString("r", invC);
        }

        public static double ConvertStringToDouble(string inString)
        {
            NumberStyles _numberStyle = NumberStyles.Any;
            var invC = CultureInfo.InvariantCulture.NumberFormat;
            double _num;
            double.TryParse(inString, _numberStyle, invC, out _num);
            return _num;
        }

        /// <summary>
        /// Use when receiving ints over the network. Converts byte array to big-endian int then to host order int format.
        /// </summary>
        /// <param name="inArray"></param>
        /// <returns></returns>
        public static int ConvertByteArrayToInt(byte[] inArray)
        {
            var convertedInt3 = BitConverter.ToInt32(inArray);
            return IPAddress.NetworkToHostOrder(convertedInt3);
        }

        public static string GetChecksum(string inFileLocation)
        {
            using (var stream = new BufferedStream(File.OpenRead(inFileLocation), 1200000))
            {
                SHA256Managed sha = new SHA256Managed();
                byte[] checksum = sha.ComputeHash(stream);
                return BitConverter.ToString(checksum).Replace("-", string.Empty);
            }
        }
    }
}

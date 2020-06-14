using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace NetworkDataTools
{
    public static class NT
    {
        public static void SendInt(Socket inHandler, int inInt)
        {
            inHandler.Send(DT.ConvertIntToByteArray(inInt));
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

        public static int ReceiveInt(Socket inHandler)
        {
            // Data buffer for incoming data.  
            var bytes = new byte[12];
            var bytesRec = 0;
            var bytesTrimmed = new byte[bytesRec];

            // Receive number of arrays to expect for next payload
            bytesRec = inHandler.Receive(bytes);
            bytesTrimmed = new byte[bytesRec];
            Array.Copy(bytes, bytesTrimmed, bytesRec);
            var numberOfArraysToExpect = DT.ConvertByteArrayToInt(bytesTrimmed);

            return numberOfArraysToExpect;
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
    }
}

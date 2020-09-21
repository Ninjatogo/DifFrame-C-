using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetworkDiscoveryAutomation
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Listen(l) or send(s)?");
            var mode = Console.ReadLine();

            if (mode.Trim() == "l")
            {
                var Server = new UdpClient(8888);
                var ResponseData = Encoding.ASCII.GetBytes("SomeResponseData");

                while (true)
                {
                    var ClientEp = new IPEndPoint(IPAddress.Any, 0);
                    var ClientRequestData = Server.Receive(ref ClientEp);
                    var ClientRequest = Encoding.ASCII.GetString(ClientRequestData);

                    Console.WriteLine("Recived {0} from {1}, sending response", ClientRequest, ClientEp.Address.ToString());
                    Server.Send(ResponseData, ResponseData.Length, ClientEp);
                }
            }
            else if (mode.Trim() == "s")
            {
                var Client = new UdpClient();
                try
                {
                    var RequestData = Encoding.ASCII.GetBytes("SomeRequestData");
                    var ServerEp = new IPEndPoint(IPAddress.Any, 0);

                    Client.EnableBroadcast = true;
                    Client.Send(RequestData, RequestData.Length, new IPEndPoint(IPAddress.Broadcast, 8888));

                    var ServerResponseData = Client.Receive(ref ServerEp);
                    var ServerResponse = Encoding.ASCII.GetString(ServerResponseData);
                    Console.WriteLine("Recived {0} from {1}", ServerResponse, ServerEp.Address.ToString());
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                finally
                {
                    Client.Close();
                }
            }

            Console.ReadLine();
        }
    }
}

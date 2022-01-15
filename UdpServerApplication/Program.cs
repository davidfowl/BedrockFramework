using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UdpServerApplication
{
    internal class Program
    {
        private const int listenPort = 5007;

        static void Main(string[] args)
        {
            UdpClient listener = new UdpClient(listenPort);
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, listenPort);

            Console.WriteLine($"UDP Server. Waiting for broadcast on {listenPort}");

            try
            {
                while (true)
                {
                    byte[] bytes = listener.Receive(ref endPoint);

                    Console.WriteLine($"Received broadcast from {endPoint} :");
                    Console.WriteLine($" {Encoding.ASCII.GetString(bytes, 0, bytes.Length)}");
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                listener.Close();
            }
        }
    }
}

using System;
using System.Net;
using System.Threading.Tasks;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;

namespace cs_dns_server_test1
{
    class Program
    {
        static void Main(string[] args)
        {
            DnsServer svr = new DnsServer(new IPEndPoint(IPAddress.Any, 54), 256, 0);

            svr.Start();

            svr.QueryReceived += Svr_QueryReceived;

            Console.Write(">");
            Console.ReadLine();

            svr.Stop();
        }

        private static async Task Svr_QueryReceived(object sender, QueryReceivedEventArgs eventArgs)
        {
            Console.WriteLine("a");
        }
    }
}

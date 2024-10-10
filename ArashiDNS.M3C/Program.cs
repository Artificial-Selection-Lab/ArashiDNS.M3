using System.Net;
using System.Text;
using ArashiDNS.M3;
using ARSoft.Tools.Net.Dns;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace ArashiDNS.M3C
{
    internal class Program
    {
        public static IPEndPoint ListenerEndPoint = new(IPAddress.Loopback, 3353);
        public static string Server = "http://localhost:5135/healthz";
        public static string SimpleKey = "M33K";
        public static bool IsV2 = true;


        static void Main(string[] args)
        {

            var dnsServer = new DnsServer(new UdpServerTransport(ListenerEndPoint),
                new TcpServerTransport(ListenerEndPoint));
            dnsServer.QueryReceived += DnsServerOnQueryReceived;
            dnsServer.Start();

            Console.WriteLine("Now listening on: " + ListenerEndPoint);
            Console.WriteLine("Application started. Press Ctrl+C / q to shut down.");
            if (!Console.IsInputRedirected && Console.KeyAvailable)
            {
                while (true)
                    if (Console.ReadKey().KeyChar == 'q')
                        Environment.Exit(0);
            }

            EventWaitHandle wait = new AutoResetEvent(false);
            while (true) wait.WaitOne();
        }

        private static async Task DnsServerOnQueryReceived(object sender, QueryReceivedEventArgs e)
        {
            if (e.Query is not DnsMessage query) return;

            var request = new HttpRequestMessage(HttpMethod.Get, Server);
            var qBytes = query.Encode().ToArraySegment(false).ToArray();
            qBytes = BrotliCompress.Compress(qBytes);
            if (IsV2)
            {
                request.Headers.Add("User-Agent", "UptimeBot/0.2");
                qBytes = Table.ConfuseBytes(qBytes,
                    Table.ConfuseString(SimpleKey, DateTime.UtcNow.ToString("mmhhdd")));
            }

            request.Headers.Add("Cookie", "NID=" + Convert
                .ToBase64String(qBytes)
                .TrimEnd('=').Replace('+', '-').Replace('/', '_'));
            var response = await new HttpClient().SendAsync(request);

            if (response.Headers.TryGetValues("Cookie", out var dataValues))
            {
                var aBytes = fromBase64StringGetBytes(dataValues.First().Split('=').Last());
                if (IsV2)
                    aBytes = Table.DeConfuseBytes(aBytes,
                        Table.ConfuseString(SimpleKey, DateTime.UtcNow.ToString("mmhhdd")));
                aBytes = BrotliCompress.Decompress(aBytes);
                e.Response = DnsMessage.Parse(aBytes);
            }
            else
            {
                var res = query.CreateResponseInstance();
                res.ReturnCode = ReturnCode.ServerFailure;
                e.Response = res;
            }
        }

        private static byte[] fromBase64StringGetBytes(string input)
        {
            var incoming = input.Replace('_', '/').Replace('-', '+');
            switch (input.Length % 4)
            {
                case 2: incoming += "=="; break;
                case 3: incoming += "="; break;
            }
            return Convert.FromBase64String(incoming);
        }
    }
}

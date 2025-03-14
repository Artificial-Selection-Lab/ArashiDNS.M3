using System.IO;
using System.Net;
using ArashiDNS.M3;
using ARSoft.Tools.Net.Dns;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;

namespace ArashiDNS.M3C
{
    internal class Program
    {
        public static IServiceProvider ServiceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
        public static IHttpClientFactory? ClientFactory = ServiceProvider.GetService<IHttpClientFactory>();

        public static IPEndPoint ListenerEndPoint = new(IPAddress.Loopback, 3353);
        public static string ServerUrl = "http://localhost:5135/healthz";
        public static string Key = "M33K";
        public static bool IsObfsed = true;
        public static string Nid = "NID";


        static void Main(string[] args)
        {
            var cmd = new CommandLineApplication
            {
                Name = "ArashiDNS.M3",
                Description = "ArashiDNS.M3 -  Like DNS over Meek, but not. " +
                              Environment.NewLine +
                              $"Copyright (c) {DateTime.Now.Year} AS-Lab. Code released under the MIT License"
            };
            cmd.HelpOption("-?|-he|--help");
            var isZh = Thread.CurrentThread.CurrentCulture.Name.Contains("zh");
            var urlArgument = cmd.Argument("target",
                isZh ? "目标 M3 服务器 URL。" : "Target M3 service URL");
            var ipOption = cmd.Option<string>("-l|--listen <IPEndPoint>",
                isZh ? "监听的地址与端口。" : "Set server listening address and port",
                CommandOptionType.SingleValue);
            var keyOption = cmd.Option<string>("-k|--key <KEY>",
                isZh ? "混淆密钥。" : "Confusion key",
                CommandOptionType.SingleValue);
            var nidOption = cmd.Option<string>("-n|--nid <NID>",
                isZh ? "NID Cookie 名称。" : "NID Cookie name",
                CommandOptionType.SingleValue);
            var noObfsOption = cmd.Option<bool>("--no-obfs",
                isZh ? "不使用混淆，仅压缩。" : "No obfs, only compression",
                CommandOptionType.NoValue);

            cmd.OnExecute(() =>
            {
                if (ipOption.HasValue())
                    ListenerEndPoint = IPEndPoint.Parse(ipOption.Value());
                if (keyOption.HasValue())
                    Key = keyOption.Value();
                if (nidOption.HasValue())
                    Nid = nidOption.Value();
                if (urlArgument.Values.Count > 0)
                    ServerUrl = urlArgument.Values[0];
                if (noObfsOption.HasValue()) IsObfsed = false;

                Console.WriteLine("Key  :" + Key);
                Console.WriteLine("URL  :" + ServerUrl);
                Console.WriteLine("Obfs :" + IsObfsed);

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
            });
            cmd.Execute(args);
        }

        private static async Task DnsServerOnQueryReceived(object sender, QueryReceivedEventArgs e)
        {
            if (e.Query is not DnsMessage query) return;

            var request = new HttpRequestMessage(HttpMethod.Get, ServerUrl);
            var qBytes = query.Encode().ToArraySegment(false).ToArray();
            qBytes = BrotliCompress.Compress(qBytes);
            if (IsObfsed)
            {
                request.Headers.Add("User-Agent", "UptimeBot/0.2");
                qBytes = Table.ConfuseBytes(qBytes,
                    Table.ConfuseString(Key, DateTime.UtcNow.ToString("mmhhdd")));
            }

            request.Headers.Add("Cookie", Nid + "=" + Convert
                .ToBase64String(qBytes)
                .TrimEnd('=').Replace('+', '-').Replace('/', '_'));

            if (ClientFactory != null)
            {
                var client = ClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("Connection", "keep-alive");
                client.DefaultRequestHeaders.Add("Keep-Alive", "600");
                var response = await client.SendAsync(request);

                if (response.Headers.TryGetValues("Cookie", out var dataValues))
                {
                    var aBytes = fromBase64StringGetBytes(dataValues.First().Split('=').Last());
                    if (IsObfsed)
                        aBytes = Table.DeConfuseBytes(aBytes,
                            Table.ConfuseString(Key, DateTime.UtcNow.ToString("mmhhdd")));
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

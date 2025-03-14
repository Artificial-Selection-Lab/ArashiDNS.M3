using System.Net;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using Microsoft.AspNetCore.WebUtilities;
using static System.String;

namespace ArashiDNS.M3
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();
            var nid = "NID";
            var path = args.FirstOrDefault(x => x.StartsWith("--path="))?.Split("=").LastOrDefault() ?? "healthz";
            var up = IPAddress.Parse(args.FirstOrDefault(x => x.StartsWith("--up="))?.Split("=").LastOrDefault() ??
                                     "8.8.8.8");
            var key = args.FirstOrDefault(x => x.StartsWith("--key="))?.Split("=").LastOrDefault() ??
                      Guid.NewGuid().ToString().Replace("-", "");
            Console.WriteLine("Key  :" + key);
            Console.WriteLine("Up   :" + up);
            Console.WriteLine("Path :" + path);
            Console.WriteLine("Do NOT use HTTPS, this breaks anti-strangeness.");

            app.Map("/" + path, async context =>
            {
                var uaStr = context.Request.Headers.TryGetValue("User-Agent", out var uaVal)
                    ? uaVal.ToString()
                    : Empty;
                if (!context.Request.Headers.TryGetValue("Cookie", out var strVal) ||
                    IsNullOrWhiteSpace(strVal.ToString()))
                {
                    await context.Response.WriteAsync("OK");
                    return;
                }

                try
                {
                    var str = strVal.ToString().Split('=').Last();
                    var qBytes = Base64UrlTextEncoder.Decode(str);
                    var isConfused = uaStr.ToLower() == "uptimebot/0.2";
                    if (isConfused)
                        qBytes = Table.DeConfuseBytes(qBytes,
                            Table.ConfuseString(key, DateTime.UtcNow.ToString("mmhhdd")));
                    qBytes = BrotliCompress.Decompress(qBytes);

                    var qMessage = DnsMessage.Parse(qBytes);
                    //Console.WriteLine(qMessage.Questions.First());
                    var aMessage = await new DnsClient(up, 5000).SendMessageAsync(qMessage);
                    if (aMessage == null)
                    {
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsync("Error");
                        return;
                    }

                    var aBytes = aMessage.Encode().ToArraySegment(false).ToArray();
                    aBytes = BrotliCompress.Compress(aBytes);
                    if (isConfused)
                        aBytes = Table.ConfuseBytes(aBytes,
                            Table.ConfuseString(key, DateTime.UtcNow.ToString("mmhhdd")));
                    context.Response.Headers.Remove("Cookie");
                    context.Response.Headers.TryAdd("Cookie",
                        nid + "=" + Base64UrlTextEncoder.Encode(aBytes));
                    await context.Response.WriteAsync("OK");
                    //Console.WriteLine(Base64UrlTextEncoder.Encode(aBytes));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    var aBytes = new DnsMessage()
                    {
                        ReturnCode = ReturnCode.ServerFailure,
                        Questions =
                        {
                            new DnsQuestion(DomainName.Parse(Guid.NewGuid().ToString()), RecordType.A, RecordClass.INet)
                        },
                        AnswerRecords = {new ARecord(DomainName.Parse(Guid.NewGuid().ToString()), 600, IPAddress.Any)}
                    }.Encode().ToArraySegment(false).ToArray();
                    context.Response.Headers.Remove("Cookie");
                    context.Response.Headers.TryAdd("Cookie",
                        nid + "=" + Base64UrlTextEncoder.Encode(Table.ConfuseBytes(aBytes,
                            Guid.NewGuid().ToString())));
                    await context.Response.WriteAsync("OK");
                }
            });

            app.Run();
        }
    }
}

using System.Net;
using System.Text;
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
            var key = "M33K";

            app.Map("/healthz", async (HttpContext context) =>
            {
                var uaStr = context.Request.Headers.TryGetValue("User-Agent", out var uaVal)
                    ? uaVal.ToString()
                    : Empty;
                if (!context.Request.Headers.TryGetValue("Cookie", out var strVal) || IsNullOrWhiteSpace(strVal.ToString()))
                {
                    await context.Response.WriteAsync("OK");
                    return;
                }

                try
                {
                    var str = strVal.ToString().Split('=').Last();
                    var qBytes = Base64UrlTextEncoder.Decode(str);
                    var isV2 = uaStr.ToLower() == "uptimebot/0.2";
                    if (isV2)
                        qBytes = Table.DeConfuseBytes(qBytes,
                            Table.ConfuseString(key, DateTime.UtcNow.ToString("mmhhdd")));

                    var qMessage = DnsMessage.Parse(qBytes);
                    Console.WriteLine(qMessage.Questions.First());
                    var aMessage = new DnsClient(IPAddress.Parse("8.8.8.8"), 5000).SendMessage(qMessage);
                    if (aMessage == null)
                    {
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsync("Error");
                        return;
                    }

                    var aBytes = aMessage.Encode().ToArraySegment(false).ToArray();
                    if (isV2)
                        aBytes = Table.ConfuseBytes(aBytes,
                            Table.ConfuseString(key, DateTime.UtcNow.ToString("mmhhdd")));
                    context.Response.Headers.Remove("Cookie");
                    context.Response.Headers.TryAdd("Cookie",
                        "NID=" + Base64UrlTextEncoder.Encode(aBytes));
                    await context.Response.WriteAsync("OK");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    var aBytes = new DnsMessage()
                    {
                        Questions =
                        {
                            new DnsQuestion(DomainName.Parse(Guid.NewGuid().ToString()), RecordType.A, RecordClass.INet)
                        },
                        AnswerRecords = {new ARecord(DomainName.Parse(Guid.NewGuid().ToString()), 600, IPAddress.Any)}
                    }.Encode().ToArraySegment(false).ToArray();
                    context.Response.Headers.Remove("Cookie");
                    context.Response.Headers.TryAdd("Cookie",
                        "NID=" + Base64UrlTextEncoder.Encode(Table.ConfuseBytes(aBytes,
                            Guid.NewGuid().ToString())));
                    await context.Response.WriteAsync("OK");
                }
            });

            app.Run();
        }
    }
}

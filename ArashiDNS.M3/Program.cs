using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ARSoft.Tools.Net.Dns;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using static System.String;

namespace ArashiDNS.M3
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();
            var key = "M3";

            app.MapGet("/healthz", (HttpContext context) =>
            {
                var uaStr = context.Request.Query.TryGetValue("User-Agent", out var uaVal)
                    ? uaVal.ToString()
                    : Empty;
                if (!context.Request.Query.TryGetValue("Cookie", out var strVal) || IsNullOrWhiteSpace(strVal))
                    return new HttpResponseMessage(HttpStatusCode.OK) {Content = new StringContent("OK")};
                try
                {
                    var str = strVal.ToString().Split('=').Last();
                    var qBytes = Base64UrlTextEncoder.Decode(str);
                    if (uaStr.ToLower() == "uptimebot/0.2")
                        qBytes = Table.DeConfuseBytes(qBytes,
                            Table.ConfuseString(key, DateTime.UtcNow.ToString("mmhhdd")));

                    var qMessage = DnsMessage.Parse(qBytes);
                    var aMessage = new DnsClient(IPAddress.Parse("8.8.8.8"), 5000).SendMessage(qMessage);
                    if (aMessage == null) return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                    var response = new HttpResponseMessage(HttpStatusCode.OK);
                    var aBytes = aMessage.Encode().ToArraySegment(false).ToArray();
                    response.Content = new StringContent("OK");
                    response.Content.Headers.Add("Cookie",
                        "NID=" + Base64UrlTextEncoder.Encode(Table.ConfuseBytes(aBytes,
                            Table.ConfuseString(key, DateTime.UtcNow.ToString("mmhhdd")))));
                    return response;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    var response = new HttpResponseMessage(HttpStatusCode.OK);
                    var aBytes = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
                    response.Content = new StringContent("OK");
                    response.Content.Headers.Add("Cookie",
                        "NID=" + Base64UrlTextEncoder.Encode(Table.ConfuseBytes(aBytes,
                            Guid.NewGuid().ToString())));
                    return response;
                }
            });

            app.Run();
        }
    }
}

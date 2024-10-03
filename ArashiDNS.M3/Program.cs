using System.Net;
using System.Net.Http.Headers;
using ARSoft.Tools.Net.Dns;
using Microsoft.AspNetCore.WebUtilities;

namespace ArashiDNS.M3
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();

            app.MapGet("/healthz", (HttpContext context) =>
            {
                if (context.Request.Query.TryGetValue("sign", out var data) && !string.IsNullOrWhiteSpace(data))
                {
                    var qMessage = DnsMessage.Parse(Base64UrlTextEncoder.Decode(data.ToString()));
                    var aMessage = new DnsClient(IPAddress.Parse("8.8.8.8"), 5000).SendMessage(qMessage);
                    if (aMessage == null) return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                    var response = new HttpResponseMessage(HttpStatusCode.OK);
                    response.Content = new StringContent("OK");
                    response.Content.Headers.Add("sign", Base64UrlTextEncoder.Encode(aMessage.Encode().ToArraySegment(false).ToArray()));
                    return response;
                }

                return new HttpResponseMessage(HttpStatusCode.OK) {Content = new StringContent("OK")};
            });

            app.Run();
        }
    }
}

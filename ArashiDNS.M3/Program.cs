using System.Net;
using System.Net.Http.Headers;

namespace ArashiDNS.M3
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddAuthorization();
            var app = builder.Build();
            app.UseAuthorization();

            app.MapGet("/healthz", (HttpContext context) =>
            {
                if (context.Request.Query.TryGetValue("sign", out var data) && !string.IsNullOrWhiteSpace(data))
                {

                }

                var response = new HttpResponseMessage();
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new StringContent("OK");
                response.Content.Headers.Add("sign", "");
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
                return response;
            });

            app.Run();
        }
    }
}

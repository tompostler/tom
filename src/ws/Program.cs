using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Unlimitedinf.Tom.WebSocket
{
    public static class Program
    {
        public sealed class Options
        {
            public string Host { get; set; } = "+";
            public int Port { get; set; } = Random.Shared.Next(49152, 65536);
            public X509Certificate2 HttpsCertificate { get; set; }
        }

        public static Task Main() => MainAsync(new());

        public static async Task MainAsync(Options options)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Environment.EnvironmentName = Environments.Development;

            // Map controllers from this assembly, regardless of where we're started
            builder.Services.AddControllers().PartManager.ApplicationParts.Add(new AssemblyPart(typeof(Controllers.WebSocketController).Assembly));

            _ = builder.WebHost.ConfigureKestrel(
                serverOptions =>
                {
                    if (options.HttpsCertificate != default)
                    {
                        serverOptions.ConfigureHttpsDefaults(httpsOptions => httpsOptions.ServerCertificate = options.HttpsCertificate);
                    }
                });


            WebApplication app = builder.Build();

            app.Urls.Add($"https://{options.Host}:{options.Port}");

            _ = app.UseWebSockets();
            _ = app.MapControllers();

            await app.RunAsync();
        }
    }
}

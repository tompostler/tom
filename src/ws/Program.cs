using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Unlimitedinf.Tom.WebSocket
{
    public static class Program
    {
        internal static string Password { get; private set; }

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

            // Establish the PSK for authorization
            Password = GeneratePassword();
            app.Logger.LogInformation($"Use the following PSK (Pre-Shared Key) for authorization to establish a web socket and browse for the lifetime of this session:\n{Password}");

            await app.RunAsync();
        }

        private static string GeneratePassword()
        {
            const string validCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            byte[] randomBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(128);

            var sb = new StringBuilder();
            foreach (byte randomByte in randomBytes)
            {
                _ = sb.Append(validCharacters[randomByte % validCharacters.Length]);
            }
            return sb.ToString();
        }
    }
}

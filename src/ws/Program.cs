using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Unlimitedinf.Tom.WebSocket
{
    public static class Program
    {
        public static Task Main() => MainAsync(new());

        public static async Task MainAsync(Options options)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Environment.EnvironmentName = Environments.Development;
            
            _ = builder.Services.AddSingleton(options);
            _ = builder.Services.AddSingleton(new Status());

            // Map controllers from this assembly, regardless of where we're started
            builder.Services.AddControllers().PartManager.ApplicationParts.Add(new AssemblyPart(typeof(Controllers.WebSocketController).Assembly));

            _ = builder.WebHost.ConfigureKestrel(
                serverOptions =>
                {
                    if (options.CustomHttpsCertificate != default)
                    {
                        serverOptions.ConfigureHttpsDefaults(httpsOptions => httpsOptions.ServerCertificate = options.CustomHttpsCertificate);
                    }
                });


            WebApplication app = builder.Build();

            app.Urls.Add($"https://{options.Host}:{options.Port}");

            _ = app.UseWebSockets();
            _ = app.MapControllers();

            if (options.CustomHttpsCertificate != default)
            {
                app.Logger.LogInformation(
                    $"Using HTTPS certificate with the following properties (echoing for manual validation on self-signed certs):\n" +
                    $"Certificate '{options.CustomHttpsCertificate.Thumbprint}' " +
                    $"with subject name '{options.CustomHttpsCertificate.Subject}' " +
                    (options.CustomHttpsCertificate.Subject == options.CustomHttpsCertificate.Issuer ? string.Empty : $"issued by '{options.CustomHttpsCertificate.Issuer}' ") +
                    $"is valid from '{options.CustomHttpsCertificate.NotBefore:yyyy-MM-dd HH:mm}' ({DateTime.Now.Subtract(options.CustomHttpsCertificate.NotBefore).TotalDays:0.00}d ago) " +
                    $"to '{options.CustomHttpsCertificate.NotAfter:yyyy-MM-dd HH:mm}' ({options.CustomHttpsCertificate.NotAfter.Subtract(DateTime.Now).TotalDays:0.00}d from now)."
                    );
            }

            // Establish the PSK for authorization
            options.Token = GenerateToken();
            app.Logger.LogInformation($"Use the following PSK (Pre-Shared Key) for authorization to establish a web socket and browse for the lifetime of this session:\n{options.Token}");

            await app.RunAsync();
        }

        private static string GenerateToken()
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

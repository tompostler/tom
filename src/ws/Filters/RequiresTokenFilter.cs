using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Unlimitedinf.Tom.WebSocket.Filters
{
    public sealed class RequiresTokenFilter : IAuthorizationFilter
    {
        private readonly Options options;
        private readonly ILogger<RequiresTokenFilter> logger;

        public RequiresTokenFilter(
            Options options,
            ILogger<RequiresTokenFilter> logger)
        {
            this.options = options;
            this.logger = logger;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            string token = default;

            // If a token url parameter is provided, that supersedes the check for the Authorization header
            if (context.HttpContext.Request.Query.TryGetValue("token", out Microsoft.Extensions.Primitives.StringValues tokenValues))
            {
                token = tokenValues.First();
                this.logger.LogInformation("Token found in URL.");
            }

            // So try to read it from the auth header
            if (string.IsNullOrWhiteSpace(token))
            {
                if (context.HttpContext.Request.Headers.TryGetValue("Authorization", out Microsoft.Extensions.Primitives.StringValues authHeaders))
                {
                    token = authHeaders.FirstOrDefault(ah => ah.StartsWith("Token "))?.Split(' ').Last();
                    this.logger.LogInformation("Token found in headers.");
                }
            }

            // No valid token
            if (string.IsNullOrWhiteSpace(token))
            {
                this.logger.LogWarning("No token found.");
                context.Result = new UnauthorizedObjectResult("No token found.");
                return;
            }

            // Invalid token
            if (!string.Equals(token, this.options.Token))
            {
                this.logger.LogWarning($"Token [{token}] does not match expected value.");
                context.Result = new UnauthorizedObjectResult("Invalid token found.");
                return;
            }

            // We successfully authenticated
        }
    }
}

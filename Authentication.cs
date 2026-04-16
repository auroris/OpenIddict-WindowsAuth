using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace IdentityServer
{
    /// <summary>
    /// Middleware that enforces authentication on all requests except the OpenID Connect
    /// discovery endpoint (<c>/.well-known/openid-configuration</c>).
    /// Unauthenticated requests receive a Negotiate/Kerberos challenge.
    /// </summary>
    public class Authentication : IMiddleware
    {
        private readonly ILogger<Authentication> _logger;

        public Authentication(ILogger<Authentication> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.User.Identity?.IsAuthenticated == true
                || context.Request.Path.StartsWithSegments("/.well-known")
                || context.Request.Path.StartsWithSegments("/connect/token"))
            {
                await next(context);
            }
            else
            {
                _logger.LogDebug("Issuing Negotiate challenge for unauthenticated {Method} {Path} from {RemoteIp}",
                    context.Request.Method, context.Request.Path,
                    context.Connection.RemoteIpAddress);
                await context.ChallengeAsync();
            }
        }
    }
}

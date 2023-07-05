using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace IdentityServer
{
    public class Authentication : IMiddleware
    {
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.User.Identity.IsAuthenticated || context.Request.Path.Value.Contains(".well-known"))
                await next(context);
            else
                await context.ChallengeAsync();
        }
    }
}

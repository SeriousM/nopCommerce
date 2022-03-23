using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.ExternalAuth.OAuth;

public class OAuthRouteProvider : IRouteProvider
{
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapControllerRoute("Plugin.ExternalAuth.OAuth.Login",
            "login",
            new
            {
                controller = "OAuthAuthentication",
                action = "Login"
            });
    }

    public int Priority => 10000;
}
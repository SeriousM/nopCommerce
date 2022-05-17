using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.ExternalAuth.OAuth;

public class OAuthRouteProvider : IRouteProvider
{
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        // Override Default Login
        endpointRouteBuilder.MapControllerRoute("Login",
            "login",
            new
            {
                controller = "OAuthAuthentication",
                action = "Login"
            });

        endpointRouteBuilder.MapControllerRoute("LoginLocal",
            "login/local",
            new
            {
                controller = "Customer",
                action = "Login"
            });

        endpointRouteBuilder.MapControllerRoute("Logout",
            "logout",
            new
            {
                controller = "OAuthAuthentication",
                action = "Logout"
            });
    }

    public int Priority => 10000;
}
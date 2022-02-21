using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Services.Authentication.External;

namespace Nop.Plugin.ExternalAuth.OAuth.Infrastructure
{
    /// <summary>
    /// Represents registrar of Facebook authentication service
    /// </summary>
    public class OAuthAuthenticationRegistrar : IExternalAuthenticationRegistrar
    {
        /// <summary>
        /// Configure
        /// </summary>
        /// <param name="builder">Authentication builder</param>
        public void Configure(AuthenticationBuilder builder)
        {
            builder.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                //set credentials
                var settings = EngineContext.Current.Resolve<OAuthExternalAuthSettings>();
                options.ClientId = settings.ClientKeyIdentifier;
                options.ClientSecret = settings.ClientSecret;

                //store access and refresh tokens for the further usage
                options.SaveTokens = true;

                //set custom events handlers
                options.Events = new OpenIdConnectEvents
                {
                    //in case of error, redirect the user to the specified URL
                    OnRemoteFailure = context =>
                    {
                        context.HandleResponse();

                        var errorUrl = context.Properties.GetString(OAuthAuthenticationDefaults.ErrorCallback);
                        context.Response.Redirect(errorUrl);

                        return Task.FromResult(0);
                    }
                };
            });
        }
    }
}
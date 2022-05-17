using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Services.Authentication;
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
            builder.AddCookie("oidc", options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.LoginPath = NopAuthenticationDefaults.LoginPath;
                options.AccessDeniedPath = NopAuthenticationDefaults.AccessDeniedPath;
            });
            builder.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                //set credentials
                var settings = EngineContext.Current.Resolve<OAuthExternalAuthSettings>();
                options.Authority = settings.AuthorityUrl;
                options.ClientId = settings.ClientKeyIdentifier;
                
                // default scopes set: openid, profile
                if (settings.AdditionalScopes is not null)
                {
                    var scopesToWrite = settings.AdditionalScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
                    foreach (var scope in scopesToWrite)
                    {
                        options.Scope.Add(scope);
                    }
                }
                
                // force using the userinfo endpoint after creating the identity (first time only).
                //options.GetClaimsFromUserInfoEndpoint

                // force https required. only during dev!
                options.RequireHttpsMetadata = false;

                options.NonceCookie.SameSite = SameSiteMode.Unspecified;
                options.CorrelationCookie.SameSite = SameSiteMode.Unspecified;

                options.SignInScheme = "oidc";

                // "metadata" address..?
                //options.MetadataAddress

                // set to "id_token" (OpenIdConnectResponseType.IdToken)
                //options.ResponseType

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
                    },
                    // TODO: validate issuer and also token itself with introspection!
                };
            });
        }
    }
}
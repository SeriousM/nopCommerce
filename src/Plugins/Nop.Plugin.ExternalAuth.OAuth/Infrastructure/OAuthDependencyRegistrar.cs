using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.ExternalAuth.OAuth.Services;
using Nop.Services.Orders;

namespace Nop.Plugin.ExternalAuth.OAuth.Infrastructure
{
    internal class OAuthDependencyRegistrar : INopStartup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IOrderProcessingService, CustomOrderProcessingService>();
        }

        public void Configure(IApplicationBuilder application)
        {
        }

        public int Order => int.MaxValue;
    }
}

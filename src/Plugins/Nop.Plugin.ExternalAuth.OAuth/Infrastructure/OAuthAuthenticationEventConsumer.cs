using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Nop.Core.Domain.Customers;
using Nop.Services.Authentication.External;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Events;

namespace Nop.Plugin.ExternalAuth.OAuth.Infrastructure
{
    /// <summary>
    /// Facebook authentication event consumer (used for saving customer fields on registration)
    /// </summary>
    public partial class OAuthAuthenticationEventConsumer 
      : IConsumer<CustomerAutoRegisteredByExternalMethodEvent>, 
        IConsumer<CustomerLoggedOutEvent>
    {
        #region Fields

        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IOptionsSnapshot<OpenIdConnectOptions> _openIdConnectOptions;

        private readonly ICustomerService _customerService;

        #endregion

        #region Ctor

        public OAuthAuthenticationEventConsumer(
          ICustomerService customerService,
          IGenericAttributeService genericAttributeService,
          IHttpContextAccessor httpContextAccessor,
          IOptionsSnapshot<OpenIdConnectOptions> openIdConnectOptions)
        {
          _customerService = customerService;
          _genericAttributeService = genericAttributeService;
          _httpContextAccessor = httpContextAccessor;
          _openIdConnectOptions = openIdConnectOptions;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task HandleEventAsync(CustomerAutoRegisteredByExternalMethodEvent eventMessage)
        {
            if (eventMessage?.Customer == null || eventMessage.AuthenticationParameters == null)
                return;

            //handle event only for this authentication method
            if (!eventMessage.AuthenticationParameters.ProviderSystemName.Equals(OAuthAuthenticationDefaults.SystemName))
                return;

            var customer = eventMessage.Customer;
            //store some of the customer fields
            var firstName = eventMessage.AuthenticationParameters.Claims?.FirstOrDefault(claim => claim.Type == ClaimTypes.GivenName)?.Value;
            if (!string.IsNullOrEmpty(firstName))
              customer.FirstName = firstName;

            var lastName = eventMessage.AuthenticationParameters.Claims?.FirstOrDefault(claim => claim.Type == ClaimTypes.Surname)?.Value;
            if (!string.IsNullOrEmpty(lastName))
              customer.LastName = lastName;

            await _customerService.UpdateCustomerAsync(customer);
        }

        public async Task HandleEventAsync(CustomerLoggedOutEvent eventMessage)
        {
            if (eventMessage?.Customer == null)
                return;

            ////var openIdConnectOptions = options.Get(OpenIdConnectDefaults.AuthenticationScheme);
            ////var configuration = await openIdConnectOptions.ConfigurationManager.GetConfigurationAsync(CancellationToken.None);
        }

        #endregion
    }
}
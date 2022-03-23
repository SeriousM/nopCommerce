using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Plugin.ExternalAuth.OAuth.Models;
using Nop.Services.Authentication.External;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.ExternalAuth.OAuth.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class OAuthAuthenticationController : BasePluginController
    {
        #region Fields

        private readonly OAuthExternalAuthSettings oAuthExternalAuthSettings;
        private readonly IAuthenticationPluginManager _authenticationPluginManager;
        private readonly IExternalAuthenticationService _externalAuthenticationService;
        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly IOptionsMonitorCache<OpenIdConnectOptions> _optionsCache;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;
        private readonly ICustomerService _customerService;
        private readonly IGenericAttributeService _genericAttributeService;

        #endregion

        #region Ctor

        public OAuthAuthenticationController(OAuthExternalAuthSettings oAuthExternalAuthSettings,
            IAuthenticationPluginManager authenticationPluginManager,
            IExternalAuthenticationService externalAuthenticationService,
            ILocalizationService localizationService,
            INotificationService notificationService,
            IOptionsMonitorCache<OpenIdConnectOptions> optionsCache,
            IPermissionService permissionService,
            ISettingService settingService,
            IStoreContext storeContext,
            IWorkContext workContext,
            ICustomerService customerService,
            IGenericAttributeService genericAttributeService
        )
        {
            this.oAuthExternalAuthSettings = oAuthExternalAuthSettings;
            _authenticationPluginManager = authenticationPluginManager;
            _externalAuthenticationService = externalAuthenticationService;
            _localizationService = localizationService;
            _notificationService = notificationService;
            _optionsCache = optionsCache;
            _permissionService = permissionService;
            _settingService = settingService;
            _storeContext = storeContext;
            _workContext = workContext;
            _customerService = customerService;
            _genericAttributeService = genericAttributeService;
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageExternalAuthenticationMethods))
                return AccessDeniedView();

            var model = new ConfigurationModel
            {
                AuthorityUrl = oAuthExternalAuthSettings.AuthorityUrl,
                ClientId = oAuthExternalAuthSettings.ClientKeyIdentifier,
                AdditionalScopes = oAuthExternalAuthSettings.AdditionalScopes
            };

            return View("~/Plugins/ExternalAuth.OAuth/Views/Configure.cshtml", model);
        }

        [HttpPost]        
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageExternalAuthenticationMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return await Configure();

            //save settings
            oAuthExternalAuthSettings.AuthorityUrl = model.AuthorityUrl;
            oAuthExternalAuthSettings.ClientKeyIdentifier = model.ClientId;
            oAuthExternalAuthSettings.AdditionalScopes = ParseScopes(model.AdditionalScopes);
            await _settingService.SaveSettingAsync(oAuthExternalAuthSettings);

            //clear OAuth authentication options cache
            _optionsCache.TryRemove(OpenIdConnectDefaults.AuthenticationScheme);

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }

        private static string ParseScopes(string scopes)
        {
            if (scopes is null or "")
            {
                return null;
            }

            scopes = string.Join(' ', scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct());

            return scopes;
        }

        public async Task<IActionResult> Login(string returnUrl)
        {
            var store = await _storeContext.GetCurrentStoreAsync();
            var methodIsAvailable = await _authenticationPluginManager
                .IsPluginActiveAsync(OAuthAuthenticationDefaults.SystemName, await _workContext.GetCurrentCustomerAsync(), store.Id);
            if (!methodIsAvailable)
                throw new NopException("OAuth authentication module cannot be loaded");

            // note: scopes and secret may be empty.

            if (string.IsNullOrEmpty(oAuthExternalAuthSettings.AuthorityUrl) ||
                string.IsNullOrEmpty(oAuthExternalAuthSettings.ClientKeyIdentifier))
            {
                throw new NopException("OAuth authentication module not configured");
            }

            //configure login callback action
            var authenticationProperties = new AuthenticationProperties
            {
                RedirectUri = Url.Action("LoginCallback", "OAuthAuthentication", new { returnUrl = returnUrl })
            };
            authenticationProperties.SetString(OAuthAuthenticationDefaults.ErrorCallback, Url.RouteUrl("Login", new { returnUrl }));

            return Challenge(authenticationProperties, OpenIdConnectDefaults.AuthenticationScheme);
        }

        public async Task<IActionResult> LoginCallback(string returnUrl)
        {
            //authenticate OAuth user
            var authenticateResult = await HttpContext.AuthenticateAsync(OpenIdConnectDefaults.AuthenticationScheme);
            if (!authenticateResult.Succeeded || !authenticateResult.Principal.Claims.Any())
                return RedirectToRoute("Login");

            //create external authentication parameters
            var authenticationParameters = new ExternalAuthenticationParameters
            {
                ProviderSystemName = OAuthAuthenticationDefaults.SystemName,
                AccessToken = await HttpContext.GetTokenAsync(OpenIdConnectDefaults.AuthenticationScheme, "id_token"),
                Email = authenticateResult.Principal.FindFirst(claim => claim.Type == ClaimTypes.Email)?.Value,
                ExternalIdentifier = authenticateResult.Principal.FindFirst(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value,
                ExternalDisplayIdentifier = authenticateResult.Principal.FindFirst(claim => claim.Type == ClaimTypes.Name)?.Value,
                Claims = authenticateResult.Principal.Claims.Select(claim => new ExternalAuthenticationClaim(claim.Type, claim.Value)).ToList()
            };

            //authenticate Nop user
            var result = await _externalAuthenticationService.AuthenticateAsync(authenticationParameters, returnUrl);

            var externalAuthenticationParameters = authenticationParameters;
            var claimsPrincipal = authenticateResult.Principal;

            await SynchronizeRolesFromClaimsAsync(externalAuthenticationParameters, claimsPrincipal);

            return result;
        }

        private async Task SynchronizeRolesFromClaimsAsync(ExternalAuthenticationParameters externalAuthenticationParameters, ClaimsPrincipal claimsPrincipal)
        {
            var customer = await _customerService.GetCustomerByEmailAsync(externalAuthenticationParameters.Email);
            var thisCustomerRoles = await _customerService.GetCustomerRolesAsync(customer);

            await SynchronizeAdminRoleFromClaimsAsync(claimsPrincipal, thisCustomerRoles, customer);
            await SynchronizeEventRolesFromClaimsAsync(claimsPrincipal,  customer);
        }

        private async Task SynchronizeAdminRoleFromClaimsAsync(ClaimsPrincipal claimsPrincipal, IEnumerable<CustomerRole> thisCustomerRoles, Customer customer)
        {
            var adminRole = await _customerService.GetCustomerRoleByIdAsync(1);

            var adminClaimExists = claimsPrincipal.FindAll(claim => claim.Type == ClaimTypes.Role
                                                                 && claim.Value == "role.shop.admin").Any();
            if (!adminClaimExists)
            {
                if (thisCustomerRoles.Any(r => r.Id == adminRole.Id))
                {
                    await _customerService.RemoveCustomerRoleMappingAsync(customer, adminRole);
                }
            }
            else
            {
                if (thisCustomerRoles.All(r => r.Id != adminRole.Id))
                {
                    await _customerService.AddCustomerRoleMappingAsync(new CustomerCustomerRoleMapping
                    {
                        CustomerId = customer.Id, CustomerRoleId = adminRole.Id
                    });
                }
            }
        }

        private async Task SynchronizeEventRolesFromClaimsAsync(ClaimsPrincipal claimsPrincipal, Customer customer)
        {
            var eventExhibitorIds = claimsPrincipal.FindAll(claim => claim.Type == "event.exhibitor")
                                                   .Select(claim => claim.Value);

            var exhibitors = eventExhibitorIds
                            .Select(ee => ee.Split('.'))
                            .Select(ee =>
                             {
                                 var eventId = ee[0];
                                 var exhibitorId = ee[1];

                                 return new ExhibitorModel
                                 {
                                     Event = eventId,
                                     Exhibitor = "Exhibitor: " + exhibitorId,
                                     ExhibitorId = exhibitorId
                                 };
                             });

            var exhibitorsString = JsonSerializer.Serialize(exhibitors);

            await _genericAttributeService.SaveAttributeAsync(customer, "Exhibitors", exhibitorsString);
        }

        #endregion
    }
}
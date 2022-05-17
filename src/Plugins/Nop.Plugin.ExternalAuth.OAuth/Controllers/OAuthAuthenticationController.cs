using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Data;
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
using IAuthenticationService = Nop.Services.Authentication.IAuthenticationService;

namespace Nop.Plugin.ExternalAuth.OAuth.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class OAuthAuthenticationController : BasePluginController
    {
        #region Fields

        private readonly OAuthExternalAuthSettings _oAuthExternalAuthSettings;
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
        private readonly IAuthenticationService _authenticationService;
        private readonly IRepository<ExternalAuthenticationRecord> _externalAuthenticationRecordRepository;

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
            IGenericAttributeService genericAttributeService,
            IAuthenticationService authenticationService,
            IRepository<ExternalAuthenticationRecord> externalAuthenticationRecordRepository)
        {
            _oAuthExternalAuthSettings = oAuthExternalAuthSettings;
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
            _authenticationService = authenticationService;
            _externalAuthenticationRecordRepository = externalAuthenticationRecordRepository;
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
                AuthorityUrl = _oAuthExternalAuthSettings.AuthorityUrl,
                ClientId = _oAuthExternalAuthSettings.ClientKeyIdentifier,
                AdditionalScopes = _oAuthExternalAuthSettings.AdditionalScopes
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

            // Save settings
            _oAuthExternalAuthSettings.AuthorityUrl = model.AuthorityUrl;
            _oAuthExternalAuthSettings.ClientKeyIdentifier = model.ClientId;
            _oAuthExternalAuthSettings.AdditionalScopes = ParseScopes(model.AdditionalScopes);
            await _settingService.SaveSettingAsync(_oAuthExternalAuthSettings);

            // Clear OAuth authentication options cache
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

        [HttpGet]
        public async Task<IActionResult> Login(string returnUrl)
        {
            var store = await _storeContext.GetCurrentStoreAsync();
            var methodIsAvailable = await _authenticationPluginManager
                .IsPluginActiveAsync(OAuthAuthenticationDefaults.SystemName, await _workContext.GetCurrentCustomerAsync(), store.Id);
            if (!methodIsAvailable)
            {
                return RedirectToAction("Login", "Customer");
            }

            // Note: scopes and secret may be empty.
            if (string.IsNullOrEmpty(_oAuthExternalAuthSettings.AuthorityUrl) ||
                string.IsNullOrEmpty(_oAuthExternalAuthSettings.ClientKeyIdentifier))
            {
                return RedirectToAction("Login", "Customer");
            }

            // Configure login callback action
            var authenticationProperties = new AuthenticationProperties
            {
                RedirectUri = Url.Action("LoginCallback", "OAuthAuthentication", new { returnUrl = returnUrl })
            };
            authenticationProperties.SetString(OAuthAuthenticationDefaults.ErrorCallback, Url.Action("Index", "Home", new { returnUrl }));

            return Challenge(authenticationProperties, OpenIdConnectDefaults.AuthenticationScheme);
        }

        public async Task<IActionResult> LoginCallback(string returnUrl)
        {
            // Authenticate OAuth user
            var authenticateResult = await HttpContext.AuthenticateAsync(OpenIdConnectDefaults.AuthenticationScheme);
            if (!authenticateResult.Succeeded || !authenticateResult.Principal.Claims.Any())
                return RedirectToAction("Index", "Home");

            // Create external authentication parameters
            var authenticationParameters = new ExternalAuthenticationParameters
            {
                ProviderSystemName = OAuthAuthenticationDefaults.SystemName,
                AccessToken = await HttpContext.GetTokenAsync(OpenIdConnectDefaults.AuthenticationScheme, OpenIdConnectParameterNames.IdToken),
                Email = authenticateResult.Principal.FindFirst(claim => claim.Type == ClaimTypes.Email)?.Value,
                ExternalIdentifier = authenticateResult.Principal.FindFirst(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value,
                ExternalDisplayIdentifier = authenticateResult.Principal.FindFirst(claim => claim.Type == ClaimTypes.Name)?.Value,
                Claims = authenticateResult.Principal.Claims.Select(claim => new ExternalAuthenticationClaim(claim.Type, claim.Value)).ToList()
            };

            // Authenticate Nop user
            var result = await _externalAuthenticationService.AuthenticateAsync(authenticationParameters, returnUrl);

            var maybeUser = await _externalAuthenticationService.GetUserByExternalAuthenticationParametersAsync(authenticationParameters);

            if (maybeUser is null)
            {
                return result;
            }
            
            await SynchronizeRolesFromClaimsAsync(authenticationParameters, authenticateResult);

            var customerAuthRecords = await _externalAuthenticationService.GetCustomerExternalAuthenticationRecordsAsync(maybeUser);
            var customerAuthRecord = customerAuthRecords.SingleOrDefault(r => r.ProviderSystemName == OAuthAuthenticationDefaults.SystemName);

            if (customerAuthRecord is not null)
            {
                customerAuthRecord.OAuthAccessToken = authenticationParameters.AccessToken;
                await _externalAuthenticationRecordRepository.UpdateAsync(customerAuthRecord);
            }

            ;

            return result;
        }

        public async Task<IActionResult> Logout()
        {
            // Retrieve id_token before signing out so we can pass it to IdentityServer for global sign out
            var currentCustomer = await _workContext.GetCurrentCustomerAsync();
            var customerExternalAuthenticationRecords = await _externalAuthenticationService.GetCustomerExternalAuthenticationRecordsAsync(currentCustomer);
            var oauthAuthenticationRecord = customerExternalAuthenticationRecords.SingleOrDefault(r => r.ProviderSystemName == OAuthAuthenticationDefaults.SystemName);

            var authenticationProperties = new AuthenticationProperties();
            authenticationProperties.SetParameter(OpenIdConnectParameterNames.IdTokenHint, oauthAuthenticationRecord?.OAuthAccessToken);

            await _authenticationService.SignOutAsync();
            return SignOut(authenticationProperties, OpenIdConnectDefaults.AuthenticationScheme);
        }

        private async Task SynchronizeRolesFromClaimsAsync(ExternalAuthenticationParameters externalAuthenticationParameters, AuthenticateResult authenticateResult)
        {
            var customer = await _customerService.GetCustomerByEmailAsync(externalAuthenticationParameters.Email);

            var claimsPrincipal = authenticateResult.Principal;

            await SynchronizeAdminRoleFromClaimsAsync(claimsPrincipal, customer);
            await SynchronizeEventRolesFromClaimsAsync(claimsPrincipal, customer);
        }

        private async Task SynchronizeAdminRoleFromClaimsAsync(ClaimsPrincipal claimsPrincipal, Customer customer)
        {
            var thisCustomerRoles = await _customerService.GetCustomerRolesAsync(customer);

            var adminRole = await _customerService.GetCustomerRoleByIdAsync(1);

            var shouldBeAdmin = claimsPrincipal.FindAll(claim => claim.Type == ClaimTypes.Role
                                                                 && claim.Value == "role.shop.admin").Any();

            var isAdmin = thisCustomerRoles.Any(r => r.Id == adminRole.Id);
            if (!shouldBeAdmin && isAdmin)
            {
                await _customerService.RemoveCustomerRoleMappingAsync(customer, adminRole);
            }
            else if (shouldBeAdmin && !isAdmin)
            {
                await _customerService.AddCustomerRoleMappingAsync(new CustomerCustomerRoleMapping
                {
                    CustomerId = customer.Id, CustomerRoleId = adminRole.Id
                });
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
                             }).ToArray();

            var customerExhibitorsString = await _genericAttributeService.GetAttributeAsync<string>(customer, "Exhibitors");
            var customerExhibitors = customerExhibitorsString is null ? null : JsonSerializer.Deserialize<ExhibitorModel[]>(customerExhibitorsString);

            var selectedExhibitor = customerExhibitors?.SingleOrDefault(e => e.IsSelected);
            var exhibitorToSelect = exhibitors.SingleOrDefault(ex => ex.ExhibitorId == selectedExhibitor?.ExhibitorId);

            if (exhibitorToSelect is not null)
            {
                exhibitorToSelect.IsSelected = true;
            }
            else
            {
                exhibitors.First().IsSelected = true;
            }

            var exhibitorsString = JsonSerializer.Serialize(exhibitors);

            await _genericAttributeService.SaveAttributeAsync(customer, "Exhibitors", exhibitorsString);
        }

        #endregion
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
        private readonly IHttpClientFactory _httpClientFactory;

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
            IRepository<ExternalAuthenticationRecord> externalAuthenticationRecordRepository,
            IHttpClientFactory httpClientFactory)
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
            _httpClientFactory = httpClientFactory;
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

            var adminRole = await _customerService.GetCustomerRoleBySystemNameAsync(NopCustomerDefaults.AdministratorsRoleName);

            var isAdmin = thisCustomerRoles.Any(r => r.Id == adminRole.Id);

            var shouldBeAdmin = claimsPrincipal.FindAll(claim => claim.Type == ClaimTypes.Role
                                                                 && claim.Value == OAuthAuthenticationDefaults.ClaimNames.ShopAdmin).Any();

            if (!shouldBeAdmin && isAdmin)
            {
                await _customerService.RemoveCustomerRoleMappingAsync(customer, adminRole);
            }

            if (shouldBeAdmin && !isAdmin)
            {
                await _customerService.AddCustomerRoleMappingAsync(new CustomerCustomerRoleMapping
                {
                    CustomerId = customer.Id,
                    CustomerRoleId = adminRole.Id
                });
            }
        }

        private async Task SynchronizeEventRolesFromClaimsAsync(ClaimsPrincipal claimsPrincipal, Customer customer)
        {
            var eventExhibitorIds = claimsPrincipal.FindAll(claim => claim.Type == OAuthAuthenticationDefaults.ClaimNames.ExhibitorEvent)
                                                   .Select(claim => claim.Value);

            var eventExhibitorModels = eventExhibitorIds
                                      .Select(ee => ee.Split('.'))
                                      .Select(ee =>
                                       {
                                           var eventId = ee[0];
                                           var exhibitorId = ee[1];

                                           return new ExhibitorModel
                                           {
                                               EventId = eventId,
                                               ExhibitorName = "ExhibitorName: " + exhibitorId,
                                               ExhibitorId = exhibitorId
                                           };
                                       }).ToList();

            if (eventExhibitorModels.Count == 0)
            {
                return;
            }

            var validEventExhibitors = new List<ExhibitorModel>();

            var httpClient = _httpClientFactory.CreateClient("ShopFunctionClient");

            foreach (var eventExhibitor in eventExhibitorModels)
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    Headers =
                {
                    { "x-functions-key", "QS-RRVqRvhz0UiozxWptJrTp1f2rshaZR96ctAnwmUOTAzFuIO49yg==" }
                },
                    RequestUri = new Uri($"https://function-shop-dev.azurewebsites.net/api/Exhibitor/{eventExhibitor.ExhibitorId}")
                };

                var response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var exhibitorResultRaw = await response.Content.ReadAsStringAsync();

                var exhibitorResult = JsonSerializer.Deserialize<ExhibitorModel>(exhibitorResultRaw);

                if (exhibitorResult == null)
                {
                    continue;
                }

                eventExhibitor.Booth = exhibitorResult.Booth;
                eventExhibitor.CompanyId = exhibitorResult.CompanyId;
                eventExhibitor.EventName = exhibitorResult.EventName;
                eventExhibitor.ExhibitorName = exhibitorResult.ExhibitorName;
                eventExhibitor.Hall = exhibitorResult.Hall;

                validEventExhibitors.Add(exhibitorResult);
            }

            var exhibitorsJson = JsonSerializer.Serialize(validEventExhibitors);

            await _genericAttributeService.SaveAttributeAsync(customer, OAuthAuthenticationDefaults.CustomAttributes.Exhibitors, exhibitorsJson);

            var customerRoles = await _customerService.GetCustomerRolesAsync(customer);
            var rolesToRemove = customerRoles.Where(r => !r.IsSystemRole);
            foreach (var role in rolesToRemove)
            {
                await _customerService.RemoveCustomerRoleMappingAsync(customer, role);
            }

            var lastSelectedExhibitorId = await _genericAttributeService.GetAttributeAsync<string>(customer, OAuthAuthenticationDefaults.CustomAttributes.SelectedExhibitorId);
            var maybeSelectedExhibitor = validEventExhibitors.SingleOrDefault(ee => ee.ExhibitorId == lastSelectedExhibitorId);
            var selectedExhibitor = maybeSelectedExhibitor ?? validEventExhibitors.First();

            await _genericAttributeService.SaveAttributeAsync(customer, OAuthAuthenticationDefaults.CustomAttributes.SelectedExhibitorId, selectedExhibitor.ExhibitorId);

            var allRoles = await _customerService.GetAllCustomerRolesAsync();
            var roleToAssign = allRoles.SingleOrDefault(r => r.SystemName == selectedExhibitor.EventId);
            if (roleToAssign is null)
            {
                return;
            }

            await _customerService.AddCustomerRoleMappingAsync(new CustomerCustomerRoleMapping
            {
                CustomerId = customer.Id,
                CustomerRoleId = roleToAssign.Id
            });
        }

        #endregion
    }
}
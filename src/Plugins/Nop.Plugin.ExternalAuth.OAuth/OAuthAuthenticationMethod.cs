using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Services.Authentication.External;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;

namespace Nop.Plugin.ExternalAuth.OAuth
{
    /// <summary>
    /// Represents method for the authentication with Facebook account
    /// </summary>
    public class OAuthAuthenticationMethod : BasePlugin, IExternalAuthenticationMethod
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;

        #endregion

        #region Ctor

        public OAuthAuthenticationMethod(ILocalizationService localizationService,
            ISettingService settingService,
            IWebHelper webHelper)
        {
            _localizationService = localizationService;
            _settingService = settingService;
            _webHelper = webHelper;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/OAuthAuthentication/Configure";
        }

        /// <summary>
        /// Gets a name of a view component for displaying plugin in public store
        /// </summary>
        /// <returns>View component name</returns>
        public string GetPublicViewComponentName()
        {
            return OAuthAuthenticationDefaults.VIEW_COMPONENT_NAME;
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task InstallAsync()
        {
            //settings
            await _settingService.SaveSettingAsync(new OAuthExternalAuthSettings());

            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.ExternalAuth.OAuth.AuthorityUrl"] = "Authority Url",
                ["Plugins.ExternalAuth.OAuth.AuthorityUrl.Hint"] = "Enter the url of the authority server. You can get it from your OAuth server administrator.",
                ["Plugins.ExternalAuth.OAuth.ClientKeyIdentifier"] = "Client ID",
                ["Plugins.ExternalAuth.OAuth.ClientKeyIdentifier.Hint"] = "Enter your client ID here. You can get it from your OAuth server administrator.",
                ["Plugins.ExternalAuth.OAuth.AdditionalScopes"] = "Additional scopes",
                ["Plugins.ExternalAuth.OAuth.AdditionalScopes.Hint"] = "Separated with space ' '. Scopes that will always be sent are 'profile' and 'openid'. You can get it from your OAuth server administrator.",
                ["Plugins.ExternalAuth.OAuth.Instructions"] = "<p>Configure the authentication with an OAuth server.</p>"
            });

            await base.InstallAsync();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task UninstallAsync()
        {
            //settings
            await _settingService.DeleteSettingAsync<OAuthExternalAuthSettings>();

            //locales
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.ExternalAuth.OAuth");

            await base.UninstallAsync();
        }

        #endregion
    }
}
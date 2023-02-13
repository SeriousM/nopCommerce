using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Plugin.ExternalAuth.OAuth.Components;
using Nop.Services.Authentication.External;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.ExternalAuth.OAuth
{
    /// <summary>
    /// Represents method for the authentication with Facebook account
    /// </summary>
    public class OAuthAuthenticationMethod : BasePlugin, IExternalAuthenticationMethod, IWidgetPlugin
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
        /// Gets a type of a view component for displaying plugin in public store
        /// </summary>
        /// <returns>View component type</returns>
        public Type GetPublicViewComponent()
        {
            return typeof(OAuthAuthenticationViewComponent);
        }

        /// <summary>
        /// Gets a value indicating whether to hide this plugin on the widget list page in the admin area
        /// </summary>
        public bool HideInWidgetList => false;
        
        /// <summary>
        /// Gets widget zones where this widget should be rendered
        /// </summary>
        /// <returns>Widget zones</returns>
        public Task<IList<string>> GetWidgetZonesAsync()
        {
            return Task.FromResult<IList<string>>(new List<string> { PublicWidgetZones.HeaderSelectors });
        }

        /// <summary>
        /// Gets a type of a view component for displaying widget
        /// </summary>
        /// <param name="widgetZone">Name of the widget zone</param>
        /// <returns>View component type</returns>
        public Type GetWidgetViewComponent(string widgetZone)
        {
          if (widgetZone is null)
            throw new ArgumentNullException(nameof(widgetZone));

            // OAuthAuthenticationDefaults.WIDGET_COMPONENT_NAME

            return typeof(OAuthAuthenticationViewComponent);
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
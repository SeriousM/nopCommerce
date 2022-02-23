using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.ExternalAuth.OAuth.Models
{
    /// <summary>
    /// Represents plugin configuration model
    /// </summary>
    public record ConfigurationModel : BaseNopModel
    {
        [NopResourceDisplayName("Plugins.ExternalAuth.OAuth.AuthorityUrl")]
        public string AuthorityUrl { get; set; }

        [NopResourceDisplayName("Plugins.ExternalAuth.OAuth.ClientKeyIdentifier")]
        public string ClientId { get; set; }

        [NopResourceDisplayName("Plugins.ExternalAuth.OAuth.AdditionalScopes")]
        public string AdditionalScopes { get; set; }
    }
}
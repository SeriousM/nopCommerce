using Nop.Core.Configuration;

namespace Nop.Plugin.ExternalAuth.OAuth
{
    /// <summary>
    /// Represents settings of the Facebook authentication method
    /// </summary>
    public class OAuthExternalAuthSettings : ISettings
    {
        /// <summary>
        /// Gets or sets OAuth2 authority (server)
        /// </summary>
        public string AuthorityUrl { get; set; }

        /// <summary>
        /// Gets or sets OAuth2 client identifier
        /// </summary>
        public string ClientKeyIdentifier { get; set; }

        /// <summary>
        /// Gets or sets additional OAuth2 scopes (space separated)
        /// </summary>
        public string AdditionalScopes { get; set; }
    }
}
namespace Nop.Plugin.ExternalAuth.OAuth
{
    /// <summary>
    /// Represents plugin constants
    /// </summary>
    public class OAuthAuthenticationDefaults
    {
        /// <summary>
        /// Gets a name of the view component to display login button
        /// </summary>
        public const string VIEW_COMPONENT_NAME = "OAuthAuthentication";

        /// <summary>
        /// Gets a name of the view component to display login button
        /// </summary>
        public const string WIDGET_COMPONENT_NAME = "ExhibitorSelector";

        /// <summary>
        /// Gets a plugin system name
        /// </summary>
        public static string SystemName = "ExternalAuth.OAuth";

        /// <summary>
        /// Gets a name of error callback method
        /// </summary>
        public static string ErrorCallback = "ErrorCallback";
    }
}
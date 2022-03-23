using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Services.Common;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.ExternalAuth.OAuth.Components
{
    /// <summary>
    /// Represents view component to display login button
    /// </summary>
    [ViewComponent(Name = OAuthAuthenticationDefaults.WIDGET_COMPONENT_NAME)]
    public class OAuthAuthenticationViOAuthExhibitorSelectViewComponent: NopViewComponent
    {
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IWorkContext _workContext;

        public OAuthAuthenticationViOAuthExhibitorSelectViewComponent(
            IGenericAttributeService genericAttributeService,
            IWorkContext workContext
            )
        {
            _genericAttributeService = genericAttributeService;
            _workContext = workContext;
        }

        /// <summary>
        /// Invoke view component
        /// </summary>
        /// <param name="widgetZone">Widget zone name</param>
        /// <param name="additionalData">Additional data</param>
        /// <returns>View component result</returns>
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var customer = await _workContext.GetCurrentCustomerAsync();
            var customerExhibitors = await _genericAttributeService.GetAttributeAsync<string>(customer, "Exhibitors");

            if (string.IsNullOrEmpty(customerExhibitors))
                return Content("");
            
            return View("~/Plugins/ExternalAuth.OAuth/Views/ExhibitorSelector.cshtml", customerExhibitors);
        }
    }
}
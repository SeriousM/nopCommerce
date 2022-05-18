using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.ExternalAuth.OAuth.Models;
using Nop.Services.Common;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.ExternalAuth.OAuth.Components
{
    /// <summary>
    /// Represents view component to display login button
    /// </summary>
    [ViewComponent(Name = OAuthAuthenticationDefaults.WIDGET_COMPONENT_NAME)]
    public class ExhibitorSelectorViewComponent : NopViewComponent
    {
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IWorkContext _workContext;

        public ExhibitorSelectorViewComponent(
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
            var customerExhibitorsString = await _genericAttributeService.GetAttributeAsync<string>(customer, OAuthAuthenticationDefaults.CustomAttributes.Exhibitors);

            if (string.IsNullOrEmpty(customerExhibitorsString)) 
                return Content("");

            var customerExhibitors = JsonSerializer.Deserialize<ExhibitorModel[]>(customerExhibitorsString);

            if (customerExhibitors is null || customerExhibitors.Length == 0)
                return Content("");

            var selectedExhibitorId = await _genericAttributeService.GetAttributeAsync<string>(customer, OAuthAuthenticationDefaults.CustomAttributes.SelectedExhibitorId);

            var model = new ExhibitorSelectorModel
            {
                Exhibitors = customerExhibitors, SelectedExhibitorId = selectedExhibitorId
            };

            return View("~/Plugins/ExternalAuth.OAuth/Views/ExhibitorSelector.cshtml", model);
        }
    }
}
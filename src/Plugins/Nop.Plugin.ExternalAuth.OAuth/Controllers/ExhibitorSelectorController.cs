using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Plugin.ExternalAuth.OAuth.Models;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.ExternalAuth.OAuth.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class ExhibitorSelectorController : BasePluginController
    {
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ICustomerService _customerService;
        private readonly IWorkContext _workContext;

        public ExhibitorSelectorController(
            IWorkContext workContext,
            IGenericAttributeService genericAttributeService,
            ICustomerService customerService
        )
        {
            _workContext = workContext;
            _genericAttributeService = genericAttributeService;
            _customerService = customerService;
        }

        [HttpGet]
        public async Task<IActionResult> SelectExhibitorAsync(string exhibitorId, string returnUrl)
        {
            var customer = await _workContext.GetCurrentCustomerAsync();
            var customerRoles = await _customerService.GetCustomerRolesAsync(customer);
            var customerExhibitorsString = await _genericAttributeService.GetAttributeAsync<string>(customer, "Exhibitors");
            var customerExhibitors = JsonSerializer.Deserialize<ExhibitorModel[]>(customerExhibitorsString);

            ExhibitorModel selectedExhibitor = null;

            foreach (var exhibitor in customerExhibitors)
            {
                if (exhibitor.ExhibitorId == exhibitorId)
                {
                    selectedExhibitor = exhibitor;
                    exhibitor.IsSelected = true;
                }
                else
                {
                    exhibitor.IsSelected = false;
                }
            }

            var serializedExhibitors = JsonSerializer.Serialize(customerExhibitors);
            await _genericAttributeService.SaveAttributeAsync(customer, "Exhibitors", serializedExhibitors);

            foreach (var roleToRemove in customerRoles.Where(r => !r.IsSystemRole))
            {
                await _customerService.RemoveCustomerRoleMappingAsync(customer, roleToRemove);
            }

            var roleToAssign = (await _customerService.GetAllCustomerRolesAsync()).SingleOrDefault(r => r.Name == selectedExhibitor?.Event);
            if (roleToAssign is null)
            {
                return Redirect(returnUrl);
            }

            await _customerService.AddCustomerRoleMappingAsync(new CustomerCustomerRoleMapping
            {
                CustomerId = customer.Id,
                CustomerRoleId = roleToAssign.Id
            });

            return Redirect(returnUrl);
        }
    }
}
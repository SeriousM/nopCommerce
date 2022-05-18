using System;
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
            var selectedExhibitorId = await _genericAttributeService.GetAttributeAsync<string>(customer, OAuthAuthenticationDefaults.CustomAttributes.SelectedExhibitorId);
            var customerExhibitorsString = await _genericAttributeService.GetAttributeAsync<string>(customer, OAuthAuthenticationDefaults.CustomAttributes.Exhibitors);
            var customerExhibitors = JsonSerializer.Deserialize<ExhibitorModel[]>(customerExhibitorsString);

            var selectedExhibitor = customerExhibitors?.SingleOrDefault(ex => ex.IdEquals(exhibitorId));

            if (selectedExhibitor is null || selectedExhibitor.IdEquals(selectedExhibitorId))
            {
                return Redirect(returnUrl);
            }

            await _genericAttributeService.SaveAttributeAsync(customer, OAuthAuthenticationDefaults.CustomAttributes.SelectedExhibitorId, selectedExhibitor.ExhibitorId);

            var rolesToRemove = customerRoles.Where(r => !r.IsSystemRole);
            foreach (var role in rolesToRemove)
            {
                await _customerService.RemoveCustomerRoleMappingAsync(customer, role);
            }

            var allRoles = await _customerService.GetAllCustomerRolesAsync();
            var roleToAssign = allRoles.SingleOrDefault(r => r.Name == selectedExhibitor.Event);
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
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.FinalInvoice.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.FinalInvoice.Components
{
    [ViewComponent(Name = "FinalInvoice")]
    public class FinalInvoiceViewComponent : NopViewComponent
    {
        private readonly FinalInvoicePaymentSettings _finalInvoicePaymentSettings;
        private readonly ILocalizationService _localizationService;
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;

        public FinalInvoiceViewComponent(FinalInvoicePaymentSettings finalInvoicePaymentSettings,
                                                ILocalizationService localizationService,
                                                IStoreContext storeContext,
                                                IWorkContext workContext)
        {
            _finalInvoicePaymentSettings = finalInvoicePaymentSettings;
            _localizationService = localizationService;
            _storeContext = storeContext;
            _workContext = workContext;
        }
        
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var store = await _storeContext.GetCurrentStoreAsync();

            var model = new PaymentInfoModel
            {
                DescriptionText = await _localizationService.GetLocalizedSettingAsync(_finalInvoicePaymentSettings,
                    x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id)
            };

            return View("~/Plugins/Payments.FinalInvoice/Views/PaymentInfo.cshtml", model);
        }

        //public IViewComponentResult Invoke()
        //{
        //    var model = new PaymentInfoModel();
        //    //set postback values (we cannot access "Form" with "GET" requests)
        //    if (Request.Method != WebRequestMethods.Http.Get)
        //    {
        //        model.DescriptionText = HttpContext.Request.Form["DescriptionText"];
        //    }

        //    return View("~/Plugins/Payments.FinalInvoice/Views/PaymentInfo.cshtml", model);
        //}
    }
}

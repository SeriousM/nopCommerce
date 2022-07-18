using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.FinalInvoice.Models
{
    public record PaymentInfoModel : BaseNopModel
    {
        [NopResourceDisplayName("Plugins.Payment.FinalInvoice.DescriptionText")]
        public string DescriptionText { get; set; }
    }
}
using System;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Tax;
using Nop.Core.Events;
using Nop.Services.Affiliates;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Discounts;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Services.Shipping;
using Nop.Services.Tax;
using Nop.Services.Vendors;

namespace Nop.Plugin.ExternalAuth.OAuth.Services
{
    public class CustomOrderProcessingService : OrderProcessingService
    {
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ICustomerService _customerService;
        private readonly IOrderService _orderService;

        public CustomOrderProcessingService(CurrencySettings currencySettings, 
                                            IAddressService addressService, 
                                            IAffiliateService affiliateService, 
                                            ICheckoutAttributeFormatter checkoutAttributeFormatter, 
                                            ICountryService countryService, 
                                            ICurrencyService currencyService, 
                                            ICustomerActivityService customerActivityService, 
                                            ICustomerService customerService, 
                                            ICustomNumberFormatter customNumberFormatter, 
                                            IDiscountService discountService, 
                                            IEncryptionService encryptionService, 
                                            IEventPublisher eventPublisher, 
                                            IGenericAttributeService genericAttributeService, 
                                            IGiftCardService giftCardService, 
                                            ILanguageService languageService, 
                                            ILocalizationService localizationService,
                                            ILogger logger,
                                            IOrderService orderService,
                                            IOrderTotalCalculationService orderTotalCalculationService,
                                            IPaymentPluginManager paymentPluginManager,
                                            IPaymentService paymentService,
                                            IPdfService pdfService,
                                            IPriceCalculationService priceCalculationService,
                                            IPriceFormatter priceFormatter,
                                            IProductAttributeFormatter productAttributeFormatter,
                                            IProductAttributeParser productAttributeParser,
                                            IProductService productService,
                                            IReturnRequestService returnRequestService,
                                            IRewardPointService rewardPointService,
                                            IShipmentService shipmentService,
                                            IShippingService shippingService,
                                            IShoppingCartService shoppingCartService,
                                            IStateProvinceService stateProvinceService,
                                            ITaxService taxService,
                                            IVendorService vendorService,
                                            IWebHelper webHelper,
                                            IWorkContext workContext,
                                            IWorkflowMessageService workflowMessageService,
                                            LocalizationSettings localizationSettings,
                                            OrderSettings orderSettings,
                                            PaymentSettings paymentSettings,
                                            RewardPointsSettings rewardPointsSettings,
                                            ShippingSettings shippingSettings,
                                            TaxSettings taxSettings) : base(currencySettings,
            addressService,
            affiliateService,
            checkoutAttributeFormatter,
            countryService,
            currencyService,
            customerActivityService,
            customerService,
            customNumberFormatter,
            discountService,
            encryptionService,
            eventPublisher,
            genericAttributeService,
            giftCardService,
            languageService,
            localizationService,
            logger,
            orderService,
            orderTotalCalculationService,
            paymentPluginManager,
            paymentService,
            pdfService,
            priceCalculationService,
            priceFormatter,
            productAttributeFormatter,
            productAttributeParser,
            productService,
            returnRequestService,
            rewardPointService,
            shipmentService,
            shippingService,
            shoppingCartService,
            stateProvinceService,
            taxService,
            vendorService,
            webHelper,
            workContext,
            workflowMessageService,
            localizationSettings,
            orderSettings,
            paymentSettings,
            rewardPointsSettings,
            shippingSettings,
            taxSettings)
        {
            _orderService = orderService;
            _genericAttributeService = genericAttributeService;
            _customerService = customerService;
        }

        public override async Task<PlaceOrderResult> PlaceOrderAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var placeOrderResult = await base.PlaceOrderAsync(processPaymentRequest);
            var placedOrder = placeOrderResult.PlacedOrder;

            var customer = await _customerService.GetCustomerByIdAsync(placedOrder.CustomerId);
            var selectedExhibitorId = await _genericAttributeService.GetAttributeAsync<string>(customer, OAuthAuthenticationDefaults.CustomAttributes.SelectedExhibitorId);

            await _genericAttributeService.SaveAttributeAsync(placedOrder, OAuthAuthenticationDefaults.CustomAttributes.OrderExhibitorId, selectedExhibitorId);
            await _orderService.InsertOrderNoteAsync(new OrderNote
            {
                OrderId = placedOrder.Id,
                Note = $"ExhibitorId set to: {selectedExhibitorId}",
                DisplayToCustomer = false,
                CreatedOnUtc = DateTime.UtcNow
            });

            return placeOrderResult;
        }
    }
}

﻿﻿using Microsoft.AspNetCore.Http;
using Smartstore.ComponentModel;
using Smartstore.Core;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Common.Services;
using Smartstore.Core.Content.MerchantStores;
using Smartstore.Core.Identity;
using Smartstore.Core.Localization.Routing;
using Smartstore.Core.Seo.Routing;
using Smartstore.Core.Stores;
using Smartstore.Web.Models.Checkout;
using Smartstore.Web.Models.Common;

namespace Smartstore.Web.Controllers
{
    public class CheckoutController : PublicController
    {
        private readonly SmartDbContext _db;
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;
        private readonly ICheckoutWorkflow _checkoutWorkflow;
        private readonly IPaymentService _paymentService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly ICheckoutStateAccessor _checkoutStateAccessor;
        private readonly OrderSettings _orderSettings;
        private readonly ShoppingCartSettings _shoppingCartSettings;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IMerchantStoreService _merchantStoreService;

        public CheckoutController(
            SmartDbContext db,
            IStoreContext storeContext,
            IWorkContext workContext,
            ICheckoutWorkflow checkoutWorkflow,
            IPaymentService paymentService,
            IShoppingCartService shoppingCartService,
            ICheckoutStateAccessor checkoutStateAccessor,
            OrderSettings orderSettings,
            ShoppingCartSettings shoppingCartSettings,
            IGenericAttributeService genericAttributeService,
            IMerchantStoreService merchantStoreService
        )
        {
            _db = db;
            _storeContext = storeContext;
            _workContext = workContext;
            _checkoutWorkflow = checkoutWorkflow;
            _paymentService = paymentService;
            _shoppingCartService = shoppingCartService;
            _checkoutStateAccessor = checkoutStateAccessor;
            _orderSettings = orderSettings;
            _shoppingCartSettings = shoppingCartSettings;
            _genericAttributeService = genericAttributeService;
            _merchantStoreService = merchantStoreService;
        }

        [DisallowRobot]
        [LocalizedRoute("/checkout", Name = "Checkout")]
        public async Task<IActionResult> Index()
        {
            var result = await _checkoutWorkflow.StartAsync(await CreateCheckoutContext());

            return result.ActionResult ?? RedirectToRoute("ShoppingCart");
        }

        public async Task<IActionResult> BillingAddress()
        {
            var context = await CreateCheckoutContext();
            var result = await _checkoutWorkflow.ProcessAsync(context);
            if (result.ActionResult != null)
            {
                return result.ActionResult;
            }

            var model = await context.MapAddressesAsync(false);

            return View(result.ViewPath, model);
        }

        [HttpPost]
        public async Task<IActionResult> SelectBillingAddress(int addressId)
        {
            var result = await _checkoutWorkflow.AdvanceAsync(
                await CreateCheckoutContext(addressId)
            );

            return result.ActionResult ?? RedirectToAction(nameof(BillingAddress));
        }

        [HttpPost, ActionName(CheckoutActionNames.BillingAddress)]
        [FormValueRequired("nextstep")]
        public async Task<IActionResult> NewBillingAddress(CheckoutAddressModel model)
        {
            var context = await CreateCheckoutContext();
            var result = await AddAddress(model, context, false);

            if (result?.ActionResult != null)
            {
                return result.ActionResult;
            }

            model = await context.MapAddressesAsync(false);

            return View(model);
        }

        [HttpPost, ActionName(CheckoutActionNames.ShippingAddress)]
        [FormValueRequired("nextstep")]
        public async Task<IActionResult> NewShippingAddress(CheckoutAddressModel model)
        {
            var context = await CreateCheckoutContext();
            var result = await AddAddress(model, context, true);

            if (result?.ActionResult != null)
            {
                return result.ActionResult;
            }

            model = await context.MapAddressesAsync(true);

            return View(model);
        }

        private async Task<CheckoutResult> AddAddress(
            CheckoutAddressModel model,
            CheckoutContext context,
            bool isShippingAddress
        )
        {
            var cart = context.Cart;
            var customer = cart.Customer;
            var ga = customer.GenericAttributes;

            if (!cart.HasItems)
            {
                return new(RedirectToRoute("ShoppingCart"));
            }

            if (!_orderSettings.AnonymousCheckoutAllowed && !customer.IsRegistered())
            {
                return new(ChallengeOrForbid());
            }

            if (ModelState.IsValid)
            {
                var address = await MapperFactory.MapAsync<AddressModel, Address>(model.NewAddress);
                customer.Addresses.Add(address);

                // Save to avoid duplicate addresses.
                await _db.SaveChangesAsync();

                if (isShippingAddress)
                {
                    customer.ShippingAddress = address;
                    if (_shoppingCartSettings.QuickCheckoutEnabled)
                    {
                        ga.DefaultShippingAddressId = customer.ShippingAddress.Id;
                    }
                }
                else
                {
                    customer.BillingAddress = address;
                    customer.ShippingAddress =
                        (model.ShippingAddressDiffers || !cart.IsShippingRequired) ? null : address;

                    var state = _checkoutStateAccessor.CheckoutState;
                    state.CustomProperties["SkipShippingAddress"] = true;
                    state.CustomProperties["ShippingAddressDiffers"] = model.ShippingAddressDiffers;

                    if (_shoppingCartSettings.QuickCheckoutEnabled)
                    {
                        ga.DefaultBillingAddressId = customer.BillingAddress.Id;
                        if (customer.ShippingAddress != null)
                        {
                            ga.DefaultShippingAddressId = customer.ShippingAddress.Id;
                        }
                    }
                }

                await _db.SaveChangesAsync();

                var result = await _checkoutWorkflow.AdvanceAsync(context);
                result.ActionResult ??= RedirectToAction(
                    isShippingAddress ? nameof(ShippingMethod) : nameof(ShippingAddress)
                );

                return result;
            }

            return null;
        }

        public async Task<IActionResult> ShippingAddress()
        {
            var context = await CreateCheckoutContext();
            var result = await _checkoutWorkflow.ProcessAsync(context);
            if (result.ActionResult != null)
            {
                return result.ActionResult;
            }

            var model = await context.MapAddressesAsync(true);

            return View(result.ViewPath, model);
        }

        [HttpPost]
        public async Task<IActionResult> SelectShippingAddress(int addressId)
        {
            var result = await _checkoutWorkflow.AdvanceAsync(
                await CreateCheckoutContext(addressId)
            );

            return result.ActionResult ?? RedirectToAction(nameof(ShippingAddress));
        }

        public async Task<IActionResult> ShippingMethod()
        {  // In your controller action for the view:
    var userAgent = Request.Headers["User-Agent"].ToString();
    bool isMobile = userAgent.Contains("Mobi") || userAgent.Contains("Android") || userAgent.Contains("iPhone");

    ViewData["IsMobile"] = isMobile;
       // Single detection point using client hints (modern) with user agent fallback
        // bool isMobileView = Request.Headers.ContainsKey("Sec-CH-UA-Mobile") 
        //     ? Request.Headers["Sec-CH-UA-Mobile"].ToString() == "?1"
        //     : Request.Headers["User-Agent"].ToString().Contains("Mobi", StringComparison.OrdinalIgnoreCase);

        // ViewData["IsMobileView"] = isMobileView;

            var context = await CreateCheckoutContext();
            // Ensure dummy shipping address before processing
            await EnsureDummyShipAddressAsync(context.Cart.Customer);

            var result = await _checkoutWorkflow.ProcessAsync(context);
            if (result.ActionResult != null)
            {
                return result.ActionResult;
            }

            var model = await MapperFactory.MapAsync<CheckoutContext, CheckoutShippingMethodModel>(
                context
            );

            result.Errors.Each(x => model.Warnings.Add(x.ErrorMessage));

            return View(result.ViewPath, model);
        }


        [HttpPost, ActionName(CheckoutActionNames.ShippingMethod)]
        [FormValueRequired("nextstep")]
        public async Task<IActionResult> SelectShippingMethod(string shippingOption)
        {
            var context = await CreateCheckoutContext(shippingOption);
            var customer = context.Cart.Customer;

            bool isGroundDelivery = shippingOption.Contains(
                "2___Shipping.FixedRate",
                StringComparison.OrdinalIgnoreCase
            );

            if (isGroundDelivery)
            {
                foreach (var organizedItem in context.Cart.Items)
                {
                    var item = organizedItem.Item;

                    // Avoid removing the item completely from the cart
                    // Just ignore the pickup fields for delivery
                    item.PickupStoreId = null;
                    item.SelectedStore = null; // Use null instead of empty string
                }

                await _db.SaveChangesAsync();

                // Set delivery-specific attributes
                customer.GenericAttributes.Set(
                    "ByGroundAddress",
                    Request.Form["ByGroundAddress"].ToString()
                );
                customer.GenericAttributes.Set(
                    "ByGroundLatitude",
                    Request.Form["ByGroundLatitude"].ToString()
                );
                customer.GenericAttributes.Set(
                    "ByGroundLongitude",
                    Request.Form["ByGroundLongitude"].ToString()
                );
                await _db.SaveChangesAsync();

                foreach (var organizedItem in context.Cart.Items)
                {
                    var item = organizedItem.Item;

                    var result = await _merchantStoreService.DeductProductQuantityForDeliveryAsync(
                        item.ProductId,
                        item.Quantity
                    );

                    if (result == null)
                    {
                        NotifyError(
                            $"Not enough stock for product {item.Product.Name} to fulfill delivery."
                        );
                        return RedirectToAction(nameof(ShippingMethod));
                    }
                }
            }
            else
            {
                // Clear ground delivery attributes when store pickup is selected
                customer.GenericAttributes.Set<string>("ByGroundAddress", null);
                customer.GenericAttributes.Set<string>("ByGroundLatitude", null);
                customer.GenericAttributes.Set<string>("ByGroundLongitude", null);
                await _db.SaveChangesAsync();
            }

            var resultWorkflow = await _checkoutWorkflow.AdvanceAsync(context);
            resultWorkflow.Errors.Take(3).Each(x => NotifyError(x.ErrorMessage));

            return resultWorkflow.ActionResult ?? RedirectToAction(nameof(ShippingMethod));
        }

        public async Task<IActionResult> PaymentMethod()
        {
            var context = await CreateCheckoutContext();
            var result = await _checkoutWorkflow.ProcessAsync(context);
            if (result.ActionResult != null)
            {
                return result.ActionResult;
            }

            var model = await MapperFactory.MapAsync<CheckoutContext, CheckoutPaymentMethodModel>(
                context
            );

            return View(result.ViewPath, model);
        }

        [HttpPost, ActionName(CheckoutActionNames.PaymentMethod)]
        [FormValueRequired("nextstep")]
        public async Task<IActionResult> SelectPaymentMethod(
            string paymentMethod,
            IFormCollection form
        )
        {
            var result = await _checkoutWorkflow.AdvanceAsync(
                await CreateCheckoutContext(paymentMethod)
            );

            result.Errors.Each(x => ModelState.AddModelError(x.PropertyName, x.ErrorMessage));

            if (!ModelState.IsValid)
            {
                return await PaymentMethod();
            }

            return result.ActionResult ?? RedirectToAction(nameof(PaymentMethod));
        }

        [HttpPost]
        public async Task<IActionResult> PaymentInfoAjax(string paymentMethodSystemName)
        {
            if (
                !_orderSettings.AnonymousCheckoutAllowed
                && !_workContext.CurrentCustomer.IsRegistered()
            )
            {
                return new EmptyResult();
            }

            var paymentMethod = await _paymentService.LoadPaymentProviderBySystemNameAsync(
                paymentMethodSystemName
            );
            if (paymentMethod == null)
            {
                return new NotFoundResult();
            }

            var infoWidget = paymentMethod.Value.GetPaymentInfoWidget();
            if (infoWidget == null)
            {
                return new EmptyResult();
            }

            try
            {
                var widgetContent = await infoWidget.InvokeAsync(
                    new WidgetContext(ControllerContext)
                );
                return Content(widgetContent.ToHtmlString().ToString());
            }
            catch (Exception ex)
            {
                // Log all but do not display inner exceptions.
                Logger.Error(ex);
                NotifyError(ex.Message);

                return new EmptyResult();
            }
        }

        public async Task<IActionResult> Confirm()
        {
            var context = await CreateCheckoutContext();
            var result = await _checkoutWorkflow.ProcessAsync(context);
            if (result.ActionResult != null)
            {
                return result.ActionResult;
            }

            var model = await MapperFactory.MapAsync<CheckoutContext, CheckoutConfirmModel>(context);

            // Add these lines to ensure ByGround fields are populated
            model.OrderReviewData.ByGroundAddress =
                context.Cart.Customer.GenericAttributes.Get<string>("ByGroundAddress");
            model.OrderReviewData.ByGroundLatitude =
                context.Cart.Customer.GenericAttributes.Get<string>("ByGroundLatitude");
            model.OrderReviewData.ByGroundLongitude =
                context.Cart.Customer.GenericAttributes.Get<string>("ByGroundLongitude");

            return View(result.ViewPath, model);
        }

        // Add this helper method inside your CheckoutController class:
        private static string GeneratePinCode(int length = 6)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Exclude similar-looking chars
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        [HttpPost, ActionName(CheckoutActionNames.Confirm)]
        public async Task<IActionResult> ConfirmOrder()
        {
            var context = await CreateCheckoutContext();
            var result = await _checkoutWorkflow.CompleteAsync(context);

            if (result.Errors.Length > 0)
            {
                var model = await MapperFactory.MapAsync<CheckoutContext, CheckoutConfirmModel>(context);
                result.Errors.Each(x => model.Warnings.Add(x.ErrorMessage));
                return View(model);
            }

            // Fetch the most recent order for the current customer
            var customer = context.Cart.Customer;
            var storeId = _storeContext.CurrentStore.Id;
            var order = await _db.Orders
                .Where(x => x.CustomerId == customer.Id && x.StoreId == storeId)
                .OrderByDescending(x => x.CreatedOnUtc)
                .FirstOrDefaultAsync();

            if (order == null)
            {
                TempData["ErrorMessage"] = "Order could not be found after creation.";
                return RedirectToAction("Confirm");
            }

            // --- PIN GENERATION LOGIC (alphanumeric, like coupon code) ---
            if (order != null)
            {
                order.OrderPin = GeneratePinCode(6); // 6-digit alphanumeric
                await _db.SaveChangesAsync();
            }

            // Redirect to ArifPay payment with the real order ID and total
            return RedirectToAction("StartPayment", "ArifPay", new { amount = order.OrderTotal, orderId = order.Id });
        }

        public async Task<IActionResult> Completed()
        {
            var store = _storeContext.CurrentStore;
            var customer = _workContext.CurrentCustomer;

            if (
                !_orderSettings.AnonymousCheckoutAllowed
                && !_workContext.CurrentCustomer.IsRegistered()
            )
            {
                return ChallengeOrForbid();
            }

            var order = await _db
                .Orders.AsNoTracking()
                .Include(x => x.Customer)
                .ApplyStandardFilter(customer.Id, store.Id)
                .OrderByDescending(x => x.CreatedOnUtc)
                .FirstOrDefaultAsync();

            if (order == null || customer.Id != order.CustomerId)
            {
                return NotFound();
            }

            if (_orderSettings.DisableOrderCompletedPage)
            {
                return RedirectToAction(
                    nameof(OrderController.Details),
                    "Order",
                    new { id = order.Id }
                );
            }

            return View(
                new CheckoutCompletedModel
                {
                    OrderId = order.Id,
                    OrderNumber = order.GetOrderNumber(),
                    Order = order,
                    OrderPin = order.OrderPin
                }
            );
        }

        private async Task<CheckoutContext> CreateCheckoutContext(object model = null)
        {
            var cart = await _shoppingCartService.GetCartAsync(
                storeId: _storeContext.CurrentStore.Id
            );

            return new(cart, HttpContext, Url) { Model = model };
        }

        private async Task EnsureDummyShipAddressAsync(Customer customer)
        {
            if (customer.ShippingAddress != null)
                return;

            var country = await _db.Countries.Where(c => c.TwoLetterIsoCode == "ET").FirstAsync();

            var dummy = new Address
            {
                Country = country,
                CountryId = country.Id,
                City = "-",
                Address1 = "N/A",
                ZipPostalCode = "-",
                PhoneNumber = "-",
                FirstName = customer.GetFullName() ?? "N/A",
                LastName = "-",
                Email = "guest.user@example.org",
            };
            customer.Addresses.Add(dummy);
            customer.ShippingAddress = dummy;

            await _db.SaveChangesAsync();
        }
    }
}

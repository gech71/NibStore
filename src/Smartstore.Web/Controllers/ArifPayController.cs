using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using System;
using Smartstore.Core.Checkout;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Web.Models.Checkout;
using Smartstore.ComponentModel;
using Smartstore.Core;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Stores;

namespace Smartstore.Web.Controllers
{
    public class ArifPayController : Controller
    {
        private readonly string _apiKey;
        private readonly ICheckoutWorkflow _checkoutWorkflow;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IStoreContext _storeContext;

        public ArifPayController(
            IOptions<ArifPaySettings> arifPaySettings,
            ICheckoutWorkflow checkoutWorkflow,
            IShoppingCartService shoppingCartService,
            IStoreContext storeContext
        )
        {
            _apiKey = arifPaySettings.Value.ApiKey;
            _checkoutWorkflow = checkoutWorkflow;
            _shoppingCartService = shoppingCartService;
            _storeContext = storeContext;
        }

        [HttpGet]
        [HttpPost]
        // [Route("arifpay/startpayment")]
        public async Task<IActionResult> StartPayment(decimal amount, string orderId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "guest";
            var nonce = Guid.NewGuid().ToString();

            var paymentPayload = new
            {
                cancelUrl = Url.Action("Cancel", "ArifPay", null, Request.Scheme),
                phone = "251954926213",
                email = "telebirrTest@gmail.com",
                nonce,
                errorUrl = Url.Action("Error", "ArifPay", null, Request.Scheme),
                notifyUrl = Url.Action("Notify", "ArifPay", new { orderId, userId }, Request.Scheme),
                successUrl = Url.Action("Completed", "Checkout", new { orderId }, Request.Scheme),
                paymentMethods = new[] { "TELEBIRR", "TELEBIRR_USSD" },
                expireDate = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ss"),
                items = new[]
                {
                    new
                    {
                        name = $"Order {orderId}",
                        quantity = 1,
                        price = amount,
                        description = "Order payment",
                        image = ""
                    }
                },
                beneficiaries = new[]
                {
                    new
                    {
                        accountNumber = "01320811436100",
                        bank = "AWINETAA",
                        amount = amount
                    }
                },
                lang = "EN"
            };

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("x-arifpay-key", _apiKey);

            var response = await httpClient.PostAsJsonAsync(
                "https://gateway.arifpay.org/api/checkout/session",
                paymentPayload
            );

            if (!response.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = "Failed to initiate payment. Please try again.";
                return RedirectToAction("Confirm", "Checkout");
            }

            var responseData = await response.Content.ReadFromJsonAsync<ArifPayResponse>();

            if (responseData != null && responseData.Error == false && !string.IsNullOrEmpty(responseData.Data.PaymentUrl))
            {
                return Redirect(responseData.Data.PaymentUrl);
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to initiate payment. Please try again.";
                return RedirectToAction("Confirm", "Checkout");
            }
        }

        [HttpGet]
        [Route("arifpay/paymentsuccess")]
        public async Task<IActionResult> PaymentSuccess(string orderId)
        {
            var context = await CreateCheckoutContext();
            var model = await MapperFactory.MapAsync<CheckoutContext, CheckoutConfirmModel>(
                context
            );
            var result = await _checkoutWorkflow.CompleteAsync(await CreateCheckoutContext());

            if (result.Errors.Length > 0)
            {
                var errorContext = await CreateCheckoutContext();
                var errorModel = await MapperFactory.MapAsync<CheckoutContext, CheckoutConfirmModel>(
                    errorContext
                );

                errorModel.OrderReviewData.ByGroundAddress =
                    errorContext.Cart.Customer.GenericAttributes.Get<string>("ByGroundAddress");
                errorModel.OrderReviewData.ByGroundLatitude =
                    errorContext.Cart.Customer.GenericAttributes.Get<string>("ByGroundLatitude");
                errorModel.OrderReviewData.ByGroundLongitude =
                    errorContext.Cart.Customer.GenericAttributes.Get<string>("ByGroundLongitude");

                result.Errors.Each(x => errorModel.Warnings.Add(x.ErrorMessage));

                return View(errorModel);
            }
            
            return result.ActionResult ?? RedirectToAction(nameof(CheckoutController.Confirm), "Checkout");
        }

        [HttpGet]
        [Route("arifpay/closetab")]
        public IActionResult CloseTab() => View();

        [HttpGet]
        [Route("arifpay/cancel")]
        public IActionResult Cancel() => View();

        [HttpGet]
        [Route("arifpay/error")]
        public IActionResult Error() => View();

        [HttpGet]
        [Route("arifpay/notify")]
        public IActionResult Notify(string orderId, string userId) => Ok();

        private async Task<CheckoutContext> CreateCheckoutContext(object model = null)
        {
            var cart = await _shoppingCartService.GetCartAsync(
                storeId: _storeContext.CurrentStore.Id
            );
            return new CheckoutContext(cart, HttpContext, Url) { Model = model };
        }

        public class ArifPayResponse
        {
            public bool Error { get; set; }
            public ArifPayData Data { get; set; }
        }

        public class ArifPayData
        {
            public string PaymentUrl { get; set; }
        }
    }
} 
﻿using System.Net.Mime;
using Smartstore.Core.Catalog;
using Smartstore.Core.Catalog.Attributes;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Checkout.Shipping;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Common.Configuration;
using Smartstore.Core.Common.Services;
using Smartstore.Core.Localization;
using Smartstore.Core.Security;
using Smartstore.Core.Seo;
using Smartstore.Engine.Modularity;
using Smartstore.Web.Models.Orders;

namespace Smartstore.Web.Controllers
{
    public class OrderController : PublicController
    {
        private readonly SmartDbContext _db;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IPaymentService _paymentService;
        private readonly IProductAttributeMaterializer _productAttributeMaterializer;
        private readonly OrderHelper _orderHelper;
        private readonly IDateTimeHelper _dateTimeHelper;
        private readonly ProductUrlHelper _productUrlHelper;
        private readonly IProviderManager _providerManager;
        private readonly OrderSettings _orderSettings;
        private readonly PdfSettings _pdfSettings;
        private readonly IShoppingCartService _shoppingCartService;

        public OrderController(
            SmartDbContext db,
            IOrderProcessingService orderProcessingService,
            IPaymentService paymentService,
            IProductAttributeMaterializer productAttributeMaterializer,
            OrderHelper orderHelper,
            IDateTimeHelper dateTimeHelper,
            IProviderManager providerManager,
            ProductUrlHelper productUrlHelper,
            OrderSettings orderSettings,
            PdfSettings pdfSettings,
            IShoppingCartService shoppingCartService
        )
        {
            _db = db;
            _orderProcessingService = orderProcessingService;
            _paymentService = paymentService;
            _productAttributeMaterializer = productAttributeMaterializer;
            _orderHelper = orderHelper;
            _dateTimeHelper = dateTimeHelper;
            _providerManager = providerManager;
            _productUrlHelper = productUrlHelper;
            _orderSettings = orderSettings;
            _pdfSettings = pdfSettings;
            _shoppingCartService = shoppingCartService;
        }

        public async Task<IActionResult> Details(int id)
        {
            // Include deleted products but do not load a deleted order.
            var order = await _db.Orders
                .AsNoTracking()
                .IgnoreQueryFilters()
                .IncludeBillingAddress()
                .IncludeOrderItems()
                .IncludeShipments()
                .FirstOrDefaultAsync(x => x.Id == id && !x.Deleted);

            if (await IsNonExistentOrderAsync(order))
            {
                return NotFound();
            }
            if (await IsUnauthorizedOrderAsync(order))
            {
                return ChallengeOrForbid();
            }

            var model = await _orderHelper.PrepareOrderDetailsModelAsync(order);
            model.ByGroundAddress = order.ByGroundAddress;
            model.ByGroundLatitude = order.ByGroundLatitude;
            model.ByGroundLongitude = order.ByGroundLongitude;
            model.OrderPin = order.OrderPin;

            return View(model);
        }

        public async Task<IActionResult> Print(int id, bool pdf = false)
        {
            var order = await _db.Orders
                .AsNoTracking()
                .IgnoreQueryFilters()
                .IncludeBillingAddress()
                .IncludeOrderItems()
                .IncludeShipments()
                .FirstOrDefaultAsync(x => x.Id == id && !x.Deleted);

            if (await IsNonExistentOrderAsync(order))
            {
                return NotFound();
            }
            if (await IsUnauthorizedOrderAsync(order))
            {
                return ChallengeOrForbid();
            }

            return await PrintCore([order], pdf);
        }

        [AuthorizeAdmin]
        [Permission(Permissions.Order.Read)]
        public async Task<IActionResult> PrintMany(string ids = null, bool pdf = false)
        {
            IList<Order> orders = null;
            var totalCount = 0;

            var orderQuery = _db.Orders
                .AsNoTracking()
                .IgnoreQueryFilters()
                .IncludeBillingAddress()
                .IncludeShippingAddress()
                .IncludeOrderItems();

            if (ids != null)
            {
                orders = await orderQuery
                    .Where(x => ids.ToIntArray().Contains(x.Id))
                    .OrderByDescending(x => x.CreatedOnUtc)
                    .ToListAsync();

                totalCount = orders.Count;
            }
            else
            {
                totalCount = await orderQuery
                    .ApplyStandardFilter()
                    .ToPagedList(0, 1)
                    .GetTotalCountAsync();

                if (totalCount > 0 && totalCount <= _pdfSettings.MaxItemsToPrint)
                {
                    orders = await orderQuery
                        .ApplyStandardFilter()
                        .ToPagedList(0, int.MaxValue)
                        .LoadAsync();
                }
            }

            if (totalCount == 0)
            {
                NotifyInfo(T("Admin.Common.ExportNoData"));
                return RedirectToReferrer();
            }

            if (totalCount > _pdfSettings.MaxItemsToPrint)
            {
                NotifyWarning(T("Admin.Common.ExportToPdf.TooManyItems", _pdfSettings.MaxItemsToPrint.ToString("N0"), totalCount.ToString("N0")));
                return RedirectToReferrer();
            }

            return await PrintCore(orders, pdf);
        }

        private async Task<IActionResult> PrintCore(IList<Order> orders, bool pdf)
        {
            if (pdf)
            {
                var (content, fileName) = await _orderHelper.GeneratePdfAsync(orders);

                return File(content, MediaTypeNames.Application.Pdf, fileName);
            }

            var model = await _orderHelper.PrepareOrderDetailsModelsAsync(orders);

            return View("Details.Print", model);
        }

        public async Task<IActionResult> ReOrder(int id)
        {
            var order = await _db.Orders
                .Include(x => x.OrderItems)
                .ThenInclude(x => x.Product)
                .FindByIdAsync(id);

            if (await IsNonExistentOrderAsync(order))
            {
                return NotFound();
            }
            if (await IsUnauthorizedOrderAsync(order))
            {
                return ChallengeOrForbid();
            }

            // Clear the current customer's cart before reordering
            var customer = Services.WorkContext.CurrentCustomer;
            var storeId = Services.StoreContext.CurrentStore.Id;
            var cart = await _shoppingCartService.GetCartAsync(customer, ShoppingCartType.ShoppingCart, storeId);

            foreach (var item in cart.Items.ToList())
            {
                await _shoppingCartService.UpdateCartItemAsync(customer, item.Item.Id, 0, null);
            }

            await _orderProcessingService.ReOrderAsync(order);

            return RedirectToRoute("ShoppingCart");
        }

        [HttpPost, ActionName("Details")]
        [FormValueRequired("repost-payment")]
        public async Task<IActionResult> RePostPayment(int id /* orderId */)
        {
            var order = await _db.Orders.FindByIdAsync(id);

            if (await IsNonExistentOrderAsync(order))
            {
                return NotFound();
            }
            if (await IsUnauthorizedOrderAsync(order))
            {
                return ChallengeOrForbid();
            }

            try
            {
                if (await _paymentService.CanRePostProcessPaymentAsync(order))
                {
                    var postProcessPaymentRequest = new PostProcessPaymentRequest
                    {
                        Order = order,
                        IsRePostProcessPayment = true
                    };

                    await _paymentService.PostProcessPaymentAsync(postProcessPaymentRequest);

                    if (postProcessPaymentRequest.RedirectUrl.HasValue())
                    {
                        return Redirect(postProcessPaymentRequest.RedirectUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                NotifyError(ex);
            }

            return RedirectToAction(nameof(Details), "Order", new { id = order.Id });
        }

        public async Task<IActionResult> ShipmentDetails(int id /* shipmentId */)
        {
            var shipment = await _db.Shipments
                .Include(x => x.Order)
                .FindByIdAsync(id);

            if (shipment == null)
            {
                return NotFound();
            }

            var order = shipment.Order;

            if (await IsNonExistentOrderAsync(order))
            {
                return NotFound();
            }
            if (await IsUnauthorizedOrderAsync(order))
            {
                return ChallengeOrForbid();
            }

            var model = await PrepareShipmentDetailsModelAsync(shipment);

            return View(model);
        }

        protected async Task<ShipmentDetailsModel> PrepareShipmentDetailsModelAsync(Shipment shipment)
        {
            Guard.NotNull(shipment);

            var order = shipment.Order ?? throw new Exception(T("Order.NotFound", shipment.OrderId));
            var store = Services.StoreContext.GetCachedStores().GetStoreById(order.StoreId) ?? Services.StoreContext.CurrentStore;
            var catalogSettings = await Services.SettingFactory.LoadSettingsAsync<CatalogSettings>(store.Id);
            var shippingSettings = await Services.SettingFactory.LoadSettingsAsync<ShippingSettings>(store.Id);

            var model = new ShipmentDetailsModel
            {
                Id = shipment.Id,
                Carrier = shipment.GenericAttributes.Get<string>("Carrier"),
                TrackingNumber = shipment.TrackingNumber,
                TrackingUrl = shipment.TrackingUrl
            };

            if (shipment.ShippedDateUtc.HasValue)
            {
                model.ShippedDate = _dateTimeHelper.ConvertToUserTime(shipment.ShippedDateUtc.Value, DateTimeKind.Utc);
            }

            if (shipment.DeliveryDateUtc.HasValue)
            {
                model.DeliveryDate = _dateTimeHelper.ConvertToUserTime(shipment.DeliveryDateUtc.Value, DateTimeKind.Utc);
            }

            var srcm = _providerManager.GetProvider<IShippingRateComputationMethod>(order.ShippingRateComputationMethodSystemName, store.Id);

            if (srcm != null && srcm.IsShippingProviderEnabled(shippingSettings))
            {
                var shipmentTracker = srcm.Value.ShipmentTracker;
                if (shipmentTracker != null)
                {
                    // The URL entered by the merchant takes precedence over an automatically generated URL.
                    if (model.TrackingUrl.IsEmpty())
                    {
                        model.TrackingUrl = shipmentTracker.GetUrl(shipment.TrackingNumber);
                    }

                    if (shippingSettings.DisplayShipmentEventsToCustomers)
                    {
                        var shipmentEvents = await shipmentTracker.GetShipmentEventsAsync(shipment.TrackingNumber);
                        if (shipmentEvents != null)
                        {
                            foreach (var shipmentEvent in shipmentEvents)
                            {
                                var shipmentEventCountry = await _db.Countries
                                    .AsNoTracking()
                                    .ApplyIsoCodeFilter(shipmentEvent.CountryCode)
                                    .FirstOrDefaultAsync();

                                var shipmentStatusEventModel = new ShipmentDetailsModel.ShipmentStatusEventModel
                                {
                                    Country = shipmentEventCountry != null ? shipmentEventCountry.GetLocalized(x => x.Name) : shipmentEvent.CountryCode,
                                    Date = shipmentEvent.Date,
                                    EventName = shipmentEvent.EventName,
                                    Location = shipmentEvent.Location
                                };

                                model.ShipmentStatusEvents.Add(shipmentStatusEventModel);
                            }
                        }
                    }
                }
            }

            // Products in this shipment.
            model.ShowSku = catalogSettings.ShowProductSku;

            foreach (var shipmentItem in shipment.ShipmentItems)
            {
                var orderItem = await _db.OrderItems
                    .Include(x => x.Product)
                    .FindByIdAsync(shipmentItem.OrderItemId, false);

                if (orderItem == null || orderItem.Product == null)
                    continue;

                var attributeCombination = await _productAttributeMaterializer.FindAttributeCombinationAsync(orderItem.Product.Id, orderItem.AttributeSelection);
                orderItem.Product.MergeWithCombination(attributeCombination);

                var shipmentItemModel = new ShipmentDetailsModel.ShipmentItemModel
                {
                    Id = shipmentItem.Id,
                    Sku = orderItem.Sku.NullEmpty() ?? orderItem.Product.Sku,
                    ProductId = orderItem.Product.Id,
                    ProductName = orderItem.Product.GetLocalized(x => x.Name),
                    ProductSeName = await orderItem.Product.GetActiveSlugAsync(),
                    AttributeInfo = orderItem.AttributeDescription,
                    QuantityOrdered = orderItem.Quantity,
                    QuantityShipped = shipmentItem.Quantity
                };

                shipmentItemModel.ProductUrl = await _productUrlHelper.GetProductUrlAsync(shipmentItemModel.ProductSeName, orderItem);

                model.Items.Add(shipmentItemModel);
            }

            model.Order = await _orderHelper.PrepareOrderDetailsModelAsync(order);
            return model;
        }

        private async Task<bool> IsNonExistentOrderAsync(Order order)
        {
            var result = order == null || order.Deleted;

            if (!await Services.Permissions.AuthorizeAsync(Permissions.Order.Read))
            {
                result = result || (order.StoreId != 0 && order.StoreId != Services.StoreContext.CurrentStore.Id);

                if (_orderSettings.DisplayOrdersOfAllStores)
                {
                    result = false;
                }
            }

            return result;
        }

        private async Task<bool> IsUnauthorizedOrderAsync(Order order)
        {
            if (!await Services.Permissions.AuthorizeAsync(Permissions.Order.Read))
            {
                return order == null || order.CustomerId != Services.WorkContext.CurrentCustomer.Id;
            }
            else
            {
                return order == null;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveForReorder(SaveForReorderModel model)
        {
            var order = _db.Orders.FirstOrDefault(x => x.Id == model.OrderId && !x.Deleted);
            if (order != null)
            {
                order.IsRecurring = true;
                order.SavedDate = DateTime.UtcNow;
                order.RecurringOrderName = model.RecurringOrderName; // Save the name
                _db.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        [HttpGet]
        public IActionResult RecurringOrders()
        {
            var customerId = Services.WorkContext.CurrentCustomer.Id;
            var recurringOrders = _db.Orders
                .Where(o => o.IsRecurring && !o.Deleted && o.CustomerId == customerId)
                .OrderByDescending(o => o.SavedDate)
                .ToList();

            return View(recurringOrders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteRecurringOrder(int orderId)
        {
            var customerId = Services.WorkContext.CurrentCustomer.Id;
            var order = _db.Orders.FirstOrDefault(x => x.Id == orderId && x.IsRecurring && !x.Deleted && x.CustomerId == customerId);
            if (order != null)
            {
                order.IsRecurring = false;
                order.SavedDate = null;
                _db.SaveChanges();
            }
            return RedirectToAction("RecurringOrders");
        }

        [HttpGet]
        public async Task<IActionResult> RecurringOrdersDetails(int id)
        {
            // Load the recurring order, including related data
            var order = await _db.Orders
                .AsNoTracking()
                .IgnoreQueryFilters()
                .IncludeBillingAddress()
                .IncludeOrderItems()
                .IncludeShipments()
                .FirstOrDefaultAsync(x => x.Id == id && x.IsRecurring && !x.Deleted);

            if (order == null)
            {
                return NotFound();
            }
            if (await IsUnauthorizedOrderAsync(order))
            {
                return ChallengeOrForbid();
            }

            var model = await _orderHelper.PrepareOrderDetailsModelAsync(order);

            return View("RecurringOrdersDetails", model);
        }

        // [HttpPost]
        // [ValidateAntiForgeryToken]
        // public IActionResult Reorder(int orderId)
        // {
        //     // Implement your reorder logic here (e.g., copy items to cart)
        //     // For now, just redirect to order details
        //     return RedirectToAction("Details", new { id = orderId });
        // }
    }

    public class SaveForReorderModel
    {
        public int OrderId { get; set; }
        public string RecurringOrderName { get; set; } // Add this line
    }
}

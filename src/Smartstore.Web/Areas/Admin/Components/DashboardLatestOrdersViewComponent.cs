using Smartstore.Admin.Models.Orders;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Identity;
using Smartstore.Core.Security;

namespace Smartstore.Admin.Components
{
    public class DashboardLatestOrdersViewComponent(
        CustomerSettings customerSettings) : SmartViewComponent
    {
        private readonly CustomerSettings _customerSettings = customerSettings;

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (!await Services.Permissions.AuthorizeAsync(Permissions.Order.Read))
            {
                return Empty();
            }

            var model = new DashboardLatestOrdersModel();
            var primaryCurrency = Services.CurrencyService.PrimaryCurrency;
            var authorizedStoreIds = await Services.StoreMappingService.GetAuthorizedStoreIdsAsync("Customer", Services.WorkContext.CurrentCustomer.Id);
            
            // Get 10 completed orders
            var completedOrders = await GetOrdersByStatus(OrderStatus.Complete, 10);
            // Get 10 processing orders
            var processingOrders = await GetOrdersByStatus(OrderStatus.Processing, 10);
            // Get 10 pending orders
            var pendingOrders = await GetOrdersByStatus(OrderStatus.Pending, 10);

            // Combine all orders
            var allOrders = completedOrders.Concat(processingOrders).Concat(pendingOrders)
                .OrderByDescending(x => x.Id)
                .ToList();

            foreach (var order in allOrders)
            {
                model.LatestOrders.Add(new()
                {
                    OrderNumber = order.OrderNumber.NullEmpty() ?? order.Id.ToString(),
                    CustomerId = order.CustomerId,
                    CustomerDisplayName = order.Customer.FormatUserName(_customerSettings, T, false, true),
                    ProductsTotal = order.OrderItems.Sum(x => x.Quantity),
                    TotalAmount = Services.CurrencyService.CreateMoney(order.OrderTotal, primaryCurrency),
                    Created = Services.DateTimeHelper.ConvertToUserTime(order.CreatedOnUtc, DateTimeKind.Utc).ToString("g"),
                    OrderState = order.OrderStatus,
                    OrderId = order.Id
                });
            }

            
            return View(model);
        }

        private async Task<List<Order>> GetOrdersByStatus(OrderStatus status, int count)
        {
            var currentUser = Services.WorkContext.CurrentCustomer;
            var authorizedStoreIds = await Services.StoreMappingService.GetAuthorizedStoreIdsAsync("Customer", currentUser.Id);
            
            var isMerchant = await Services.DbContext.CustomerRoleMappings
                .AnyAsync(m => m.CustomerId == currentUser.Id && 
                             m.CustomerRole.SystemName == "Merchant");

            IQueryable<Order> query = Services.DbContext.Orders
                .ApplyCustomerFilter(authorizedStoreIds)
                .AsNoTracking()
                .AsSplitQuery();

            if (isMerchant)
            {
                var merchantProductIds = await Services.DbContext.GenericAttributes
                    .Where(a => a.KeyGroup == "Product" &&
                              a.Key == "CreatedByUserId" &&
                              a.Value == currentUser.Id.ToString())
                    .Select(a => a.EntityId)
                    .ToListAsync();

                if (!merchantProductIds.Any())
                {
                    return new List<Order>();
                }

                query = query.Where(o => o.OrderItems
                    .Any(oi => merchantProductIds.Contains(oi.ProductId)));
            }
            var orders = await query
                .Include(x => x.Customer)
                    .ThenInclude(x => x.CustomerRoleMappings)
                    .ThenInclude(x => x.CustomerRole)
                .Include(x => x.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.ProductManufacturers)
                            .ThenInclude(pm => pm.Manufacturer)
                .OrderByDescending(x => x.Id)
                .ToListAsync();
            
            return orders
                .Where(x => x.OrderStatus == status)
                .Take(count)
                .ToList();
        }
    }
}

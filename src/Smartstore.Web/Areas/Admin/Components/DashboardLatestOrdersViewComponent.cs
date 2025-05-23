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
            var authorizedStoreIds = await Services.StoreMappingService.GetAuthorizedStoreIdsAsync("Customer", Services.WorkContext.CurrentCustomer.Id);
            
            // First get all orders with the required includes
            var query = Services.DbContext.Orders
                .ApplyCustomerFilter(authorizedStoreIds)
                .AsNoTracking()
                .AsSplitQuery()
                .Include(x => x.Customer)
                    .ThenInclude(x => x.CustomerRoleMappings)
                    .ThenInclude(x => x.CustomerRole)
                .Include(x => x.OrderItems);
                
            // Execute the query and materialize the results
            var allOrders = await query.ToListAsync();
            
            // Filter by status in memory and take the specified count
            return allOrders
                .Where(x => x.OrderStatus == status)
                .OrderByDescending(x => x.Id)
                .Take(count)
                .ToList();
        }
    }
}

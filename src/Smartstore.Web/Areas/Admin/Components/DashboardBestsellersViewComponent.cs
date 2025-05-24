using Smartstore.Admin.Models.Orders;
using Smartstore.Core.Checkout.Orders.Reporting;
using Smartstore.Core.Security;

namespace Smartstore.Admin.Components
{
    public class DashboardBestsellersViewComponent : SmartViewComponent
    {
        private readonly SmartDbContext _db;

        public DashboardBestsellersViewComponent(SmartDbContext db)
        {
            _db = db;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (!await Services.Permissions.AuthorizeAsync(Permissions.Order.Read))
            {
                return Empty();
            }

            var currentUser = Services.WorkContext.CurrentCustomer;
            var authorizedStoreIds = await Services.StoreMappingService.GetAuthorizedStoreIdsAsync("Customer", currentUser.Id);

            const int pageSize = 7;

            var isMerchant = await _db.CustomerRoleMappings
                .AnyAsync(m => m.CustomerId == currentUser.Id &&
                             m.CustomerRole.SystemName == "Merchant");

            var orderItemQuery = from oi in _db.OrderItems.AsNoTracking()
                                 join o in _db.Orders.ApplyCustomerFilter(authorizedStoreIds).AsNoTracking() on oi.OrderId equals o.Id
                                 join p in _db.Products.AsNoTracking() on oi.ProductId equals p.Id
                                 where !p.IsSystemProduct
                                 select new { OrderItem = oi, ProductId = p.Id };

            if (isMerchant)
            {
                var merchantProductIds = await _db.GenericAttributes
                    .Where(a => a.KeyGroup == "Product" &&
                              a.Key == "CreatedByUserId" &&
                              a.Value == currentUser.Id.ToString())
                    .Select(a => a.EntityId)
                    .ToListAsync();

                if (merchantProductIds.Any())
                {
                    orderItemQuery = orderItemQuery.Where(x => merchantProductIds.Contains(x.ProductId));
                }
                else
                {
                    return View(new DashboardBestsellersModel
                    {
                        BestsellersByQuantity = new List<BestsellersReportLineModel>(),
                        BestsellersByAmount = new List<BestsellersReportLineModel>()
                    });
                }
            }

            var finalOrderItemQuery = orderItemQuery.Select(x => x.OrderItem);

            var reportByQuantity = await finalOrderItemQuery
                .AsNoTracking()
                .SelectAsBestsellersReportLine(ReportSorting.ByQuantityDesc)
                .Take(pageSize)
                .ToListAsync();

            var reportByAmount = await finalOrderItemQuery
                .AsNoTracking()
                .SelectAsBestsellersReportLine(ReportSorting.ByAmountDesc)
                .Take(pageSize)
                .ToListAsync();

            var model = new DashboardBestsellersModel
            {
                BestsellersByQuantity = await reportByQuantity.MapAsync(Services),
                BestsellersByAmount = await reportByAmount.MapAsync(Services)
            };

            return View(model);
        }
    }
}
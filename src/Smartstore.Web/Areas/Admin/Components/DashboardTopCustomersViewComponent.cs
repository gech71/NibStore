using Smartstore.Admin.Models.Customers;
using Smartstore.Core.Checkout.Orders.Reporting;
using Smartstore.Core.Security;

namespace Smartstore.Admin.Components
{
    public class DashboardTopCustomersViewComponent : SmartViewComponent
    {
        private const int NUM_REPORT_LINES = 7;

        private readonly SmartDbContext _db;

        public DashboardTopCustomersViewComponent(SmartDbContext db)
        {
            _db = db;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (!await Services.Permissions.AuthorizeAsync(Permissions.Customer.Read))
            {
                return Empty();
            }

            var currentUser = Services.WorkContext.CurrentCustomer;
            var authorizedStoreIds = await Services.StoreMappingService.GetAuthorizedStoreIdsAsync("Customer", currentUser.Id);

            var orderQuery = _db.Orders.Where(x => !x.Customer.Deleted).ApplyCustomerFilter(authorizedStoreIds);

            var isMerchant = await _db.CustomerRoleMappings
                .AnyAsync(m => m.CustomerId == currentUser.Id &&
                             m.CustomerRole.SystemName == "Merchant");

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
                    orderQuery = orderQuery.Where(o => o.OrderItems.Any(oi => merchantProductIds.Contains(oi.ProductId)));
                }
                else
                {
                    return View(new DashboardTopCustomersModel
                    {
                        TopCustomersByQuantity = new List<TopCustomerReportLineModel>(),
                        TopCustomersByAmount = new List<TopCustomerReportLineModel>()
                    });
                }
            }

            var reportByQuantity = await orderQuery
                .SelectAsTopCustomerReportLine(ReportSorting.ByQuantityDesc)
                .Take(NUM_REPORT_LINES)
                .ToListAsync();

            var reportByAmount = await orderQuery
                .SelectAsTopCustomerReportLine(ReportSorting.ByAmountDesc)
                .Take(NUM_REPORT_LINES)
                .ToListAsync();

            var model = new DashboardTopCustomersModel
            {
                TopCustomersByQuantity = await reportByQuantity.MapAsync(_db),
                TopCustomersByAmount = await reportByAmount.MapAsync(_db)
            };

            return View(model);
        }
    }
}
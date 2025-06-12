using Smartstore.Admin.Models.Catalog;

namespace Smartstore.Admin.Models.Discounts
{
    public class ApplyDiscountViewModel
{
    public DiscountModel Discount { get; set; }
    public ProductListModel ProductList { get; set; }
    public ProductOverviewModel ProductOverview { get; set; }

}
}
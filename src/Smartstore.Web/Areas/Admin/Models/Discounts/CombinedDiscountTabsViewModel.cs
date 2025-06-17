using Smartstore.Admin.Models.Catalog;

namespace Smartstore.Admin.Models.Discounts
{
    public class CombinedDiscountTabsViewModel
    {
        public DiscountModel Discount { get; set; }
        public ProductListModel ProductList { get; set; }
        public DiscountListModel ProductsWithDiscounts { get; set; }
    }
}
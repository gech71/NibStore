using Smartstore.Admin.Models.Catalog;

namespace Smartstore.Admin.Models.Discounts
{
    public class CombinedDiscountTabsViewModel
    {
        public DiscountListModel DiscountList { get; set; }
        public ProductListModel ProductList { get; set; }
        public ProductDiscountViewModel ProductDiscountList { get; set; }

    }
}
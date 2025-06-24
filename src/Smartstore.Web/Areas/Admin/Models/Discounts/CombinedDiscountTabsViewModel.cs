using Smartstore.Admin.Models.Catalog;
using Smartstore.Web.Models.DataGrid;


namespace Smartstore.Admin.Models.Discounts
{
    public class CombinedDiscountTabsViewModel
    {
        public DiscountListModel DiscountList { get; set; }
        public ProductListModel ProductList { get; set; }
        public GridModel<ProductDiscountViewModel> ProductDiscounts { get; set; }

    }
}
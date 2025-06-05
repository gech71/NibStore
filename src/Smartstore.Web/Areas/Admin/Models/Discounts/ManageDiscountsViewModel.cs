namespace Smartstore.Admin.Models.Discounts
{
    public class ManageDiscountsViewModel
    {
        public DiscountModel DiscountModel { get; set; }
        public DiscountListModel DiscountListModel { get; set; }
        public DiscountProductListModel DiscountProductListModel { get; set; }
        public IEnumerable<dynamic> ProductsWithDiscounts { get; set; }
    }
}
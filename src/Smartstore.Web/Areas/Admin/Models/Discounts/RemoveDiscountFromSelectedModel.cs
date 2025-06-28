namespace Smartstore.Admin.Models.Discounts
{
    public class RemoveDiscountFromSelectedModel
    {
        public int[] SelectedIds { get; set; }
        public int DiscountId { get; set; }
    }
}
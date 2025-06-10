namespace Smartstore.Admin.Models.Discounts
{
    public class ProductWithDiscountModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; }
    public string DiscountName { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public decimal? DiscountAmount { get; set; }
}
}
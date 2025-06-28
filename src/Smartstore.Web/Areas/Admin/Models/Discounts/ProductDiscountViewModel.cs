using System;

namespace Smartstore.Admin.Models.Discounts
{
    public class ProductDiscountViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public int DiscountId { get; set; }
        public string DiscountName { get; set; }
        public decimal DiscountPercentage { get; set; }
        public decimal DiscountAmount { get; set; }
        public DateTime? StartDateUtc { get; set; }
        public DateTime? EndDateUtc { get; set; }
    }
} 
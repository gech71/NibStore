using System;
using System.Collections.Generic;
using Smartstore.Core.Localization;
using Smartstore.Web.Models.Common;

namespace Smartstore.Admin.Models.Discounts
{
    public class ManageDiscountsViewModel
    {
        public DiscountListModel DiscountListModel { get; set; }
        public DiscountProductListModel DiscountProductListModel { get; set; }
        public List<ProductWithDiscountModel> ProductsWithDiscounts { get; set; }
        public DiscountModel DiscountModel { get; set; }
    }

    public class DiscountProductListModel : ModelBase
    {
        public List<DiscountProductModel> Products { get; set; } = new();
        public List<DiscountModel> AvailableDiscounts { get; set; } = new();
    }

    public class DiscountProductModel : EntityModelBase
    {
        [LocalizedDisplay("*Name")]
        public string Name { get; set; }
        [LocalizedDisplay("*Sku")]
        public string Sku { get; set; }
        [LocalizedDisplay("*Price")]
        public string Price { get; set; }
        [LocalizedDisplay("*StockQuantity")]
        public int StockQuantity { get; set; }
        [LocalizedDisplay("*Published")]
        public bool Published { get; set; }
        public int ProductTypeId { get; set; }
        public string ProductTypeName { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
    }

    public class ProductWithDiscountModel : ModelBase
    {
        public string ProductName { get; set; }
        public string DiscountName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? DiscountPercentage { get; set; }
        public decimal? DiscountAmount { get; set; }
    }
}
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Localization;
using Smartstore.Web.Models.Common;
using Smartstore.Web.Models.DataGrid;


namespace Smartstore.Admin.Models.Discounts
{
    [LocalizedDisplay("Admin.Catalog.Products.List.")]
    public class DiscountProductModel : EntityModelBase
    {
        [LocalizedDisplay("*Fields.Name")]
        public string Name { get; set; }

        [LocalizedDisplay("*Fields.Sku")]
        public string Sku { get; set; }

        [LocalizedDisplay("*Fields.Price")]
        public string Price { get; set; }

        [LocalizedDisplay("*Fields.StockQuantity")]
        public int StockQuantity { get; set; }

        [LocalizedDisplay("*Fields.Published")]
        public bool Published { get; set; }

        public int ProductTypeId { get; set; }
        public string ProductTypeName { get; set; }
        public string ProductTypeLabelHint
        {
            get
            {
                return ProductTypeId switch
                {
                    (int)ProductType.SimpleProduct => "secondary",
                    (int)ProductType.GroupedProduct => "info",
                    (int)ProductType.BundledProduct => "success",
                    _ => "secondary",
                };
            }
        }

        public DateTime? CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
    }

   public class DiscountProductListModel : ModelBase
{
    public bool IsSingleStoreMode { get; set; }
    public List<DiscountProductModel> Products { get; set; } = new();
    public List<DiscountModel> AvailableDiscounts { get; set; } = new(); // <-- Use your DiscountModel here
}

    public class DiscountProductQueryModel 
    {
        public string SearchProductName { get; set; }
        public string SearchSku { get; set; }
        public int? SearchCategoryId { get; set; }
        public int? SearchManufacturerId { get; set; }
        public int? SearchStoreId { get; set; }
        public bool? SearchIsPublished { get; set; }
        public int? SearchProductTypeId { get; set; }
        public bool? SearchWithoutDiscount { get; set; } = true;
    }
}
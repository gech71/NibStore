// using System;
// using System.Collections.Generic;
// using Smartstore.Core.Catalog.Products;
// using Smartstore.Core.Localization;
// using Smartstore.Web.Models.Common;
// namespace Smartstore.Admin.Models.Discounts
// {
//     public class ManageDiscountsViewModel : ModelBase
//     {
//         public DiscountListModel DiscountListModel { get; set; } = new();
//         public DiscountProductModel Product { get; set; } // single product, not a list
//         public DiscountModel DiscountModel { get; set; } = new();
//         public ProductWithDiscountModel ProductWithDiscount { get; set; } // single product-with-discount
//         public List<DiscountModel> AvailableDiscounts { get; set; } = new();

//         // Added properties to fix missing definitions
//         public List<DiscountProductModel> Products { get; set; } = new();
//         public List<ProductWithDiscountModel> ProductsWithDiscounts { get; set; } = new();
//     }
// }
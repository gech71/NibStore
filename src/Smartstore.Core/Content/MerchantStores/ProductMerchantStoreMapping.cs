using System.ComponentModel.DataAnnotations.Schema;
using Smartstore.Core.Catalog.Products;

namespace Smartstore.Core.Content.MerchantStores
{
    [Table("Product_MerchantStore_Mapping")]
    public class ProductMerchantStoreMapping : BaseEntity
    {
        public int ProductId { get; set; }
        public int MerchantStoreId { get; set; }
        public int Quantity { get; set; }
        public int DeliveryPriority { get; set; }
        public int DisplayOrder { get; set; }
        public virtual Product Product { get; set; }
        public virtual MerchantStore MerchantStore { get; set; }
    }
}

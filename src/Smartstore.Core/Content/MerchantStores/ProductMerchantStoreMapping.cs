using Smartstore.Core.Catalog.Products;
using System.ComponentModel.DataAnnotations.Schema;

namespace Smartstore.Core.Content.MerchantStores
{
    [Table("Product_MerchantStore_Mapping")]
    public class ProductMerchantStoreMapping : BaseEntity
    {
        public int ProductId { get; set; }
        public int MerchantStoreId { get; set; }
        public int DisplayOrder { get; set; }

        public virtual Product Product { get; set; }
        public virtual MerchantStore MerchantStore { get; set; }
    }
}

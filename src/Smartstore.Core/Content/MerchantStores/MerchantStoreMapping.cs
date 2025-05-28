using Smartstore.Core.Catalog.Products;
using System.ComponentModel.DataAnnotations.Schema;
namespace Smartstore.Core.Content.MerchantStores
{
    [Table("MerchantStoreMapping")]
    public class MerchantStoreMapping : BaseEntity
    {
        public int EntityId { get; set; }
        public string EntityName { get; set; }
        public int MerchantStoreId { get; set; }

        public MerchantStore MerchantStore { get; set; }
    }
}
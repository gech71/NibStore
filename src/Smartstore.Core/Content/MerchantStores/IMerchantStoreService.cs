using System.Collections.Generic;
using System.Threading.Tasks;

namespace Smartstore.Core.Content.MerchantStores
{
    public interface IMerchantStoreService
    {
        Task<IList<MerchantStore>> GetAllMerchantStoresAsync(bool includeHidden = false);
        Task<MerchantStore> GetMerchantStoreByIdAsync(int merchantStoreId);
        Task InsertMerchantStoreAsync(MerchantStore merchantStore);
        Task UpdateMerchantStoreAsync(MerchantStore merchantStore);
        Task DeleteMerchantStoreAsync(MerchantStore merchantStore);
        Task InsertProductMerchantStoreMappingAsync(ProductMerchantStoreMapping mapping);
        Task DeleteProductMerchantStoreMappingAsync(ProductMerchantStoreMapping mapping);
        Task<IList<ProductMerchantStoreMapping>> GetProductMerchantStoreMappingsByProductIdAsync(
            int productId
        );
        Task<bool> ExistsProductMerchantStoreMappingAsync(int productId, int merchantStoreId);
        Task<List<(int MerchantStoreId, int Deducted)>> DeductProductQuantityForDeliveryAsync(int productId, int quantity);

    }
}

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
    }
}
using System.Collections.Generic;
using System.Threading.Tasks;
using Smartstore.Core.Content.MerchantStores;

namespace Smartstore.Core.Common.Services
{
    public interface IStoreProductService
    {
        /// <summary>
        /// Get only the names of stores that offer the given product.
        /// </summary>
        Task<IList<string>> GetStoresForProductAsync(int productId);
         // New method with quantity filter
      

        /// <summary>
        /// Get full store details (address, hours, etc.) for a given product.
        /// </summary>
        Task<IList<MerchantStore>> GetStoresForProductAsync(int productId,int requiredQuantity);
    }
}
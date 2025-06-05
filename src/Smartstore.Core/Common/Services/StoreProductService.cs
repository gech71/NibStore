using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Smartstore.Core.Data;
using Smartstore.Core.Content.MerchantStores;

namespace Smartstore.Core.Common.Services
{
    public class StoreProductService : IStoreProductService
    {
        private readonly SmartDbContext _db;

        public StoreProductService(SmartDbContext db)
        {
            _db = db;
        }

       
        public async Task<IList<string>> GetStoresForProductAsync(int productId)
        {
            var storeNames = await _db.Set<ProductMerchantStoreMapping>()
                .Include(p => p.MerchantStore)
                .Where(p => p.ProductId == productId && p.MerchantStore.Published)
                .OrderBy(p => p.MerchantStore.DisplayOrder)
                .Select(p => p.MerchantStore.Name)
                .ToListAsync();

            return storeNames;
        }

       
        public async Task<IList<MerchantStore>> GetStoresForProductAsync(int productId, int requiredQuantity)
        {
            var stores = await _db.Set<ProductMerchantStoreMapping>()
                .Include(p => p.MerchantStore)
                .Where(p =>
                    p.ProductId == productId &&
                    p.MerchantStore.Published &&
                    p.Quantity >= requiredQuantity)
                .OrderBy(p => p.MerchantStore.DisplayOrder)
                .Select(p => p.MerchantStore)
                .ToListAsync();

            return stores;
        }
    }
}
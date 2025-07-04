using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Smartstore.Core.Content.MerchantStores;
using Smartstore.Core.Data;
using Smartstore.Core.Rules;
using Smartstore.Data;

namespace Smartstore.Core.Content.MerchantStores
{
    public class MerchantStoreService : IMerchantStoreService
    {
        private readonly SmartDbContext _db;

        public MerchantStoreService(SmartDbContext db)
        {
            _db = db;
        }

        public async Task<IList<MerchantStore>> GetAllMerchantStoresAsync(
            bool includeHidden = false
        )
        {
            var query = _db.MerchantStores.AsQueryable();

            if (!includeHidden)
            {
                query = query.Where(x => x.Published);
            }

            return await query.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToListAsync();
        }

        public async Task<MerchantStore> GetMerchantStoreByIdAsync(int merchantStoreId)
        {
            return await _db.MerchantStores.FindByIdAsync(merchantStoreId);
        }

        public async Task InsertMerchantStoreAsync(MerchantStore merchantStore)
        {
            Guard.NotNull(merchantStore, nameof(merchantStore));

            _db.MerchantStores.Add(merchantStore);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateMerchantStoreAsync(MerchantStore merchantStore)
        {
            Guard.NotNull(merchantStore, nameof(merchantStore));

            _db.MerchantStores.Update(merchantStore);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteMerchantStoreAsync(MerchantStore merchantStore)
        {
            Guard.NotNull(merchantStore, nameof(merchantStore));

            _db.MerchantStores.Remove(merchantStore);
            await _db.SaveChangesAsync();
        }

        public async Task InsertProductMerchantStoreMappingAsync(
            ProductMerchantStoreMapping mapping
        )
        {
            _db.Set<ProductMerchantStoreMapping>().Add(mapping);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteProductMerchantStoreMappingAsync(
            ProductMerchantStoreMapping mapping
        )
        {
            _db.Set<ProductMerchantStoreMapping>().Remove(mapping);
            await _db.SaveChangesAsync();
        }

        public async Task<
            IList<ProductMerchantStoreMapping>
        > GetProductMerchantStoreMappingsByProductIdAsync(int productId)
        {
            return await _db.Set<ProductMerchantStoreMapping>()
                .Where(pms => pms.ProductId == productId)
                .ToListAsync();
        }

        public async Task<bool> ExistsProductMerchantStoreMappingAsync(
            int productId,
            int merchantStoreId
        )
        {
            return await _db.Set<ProductMerchantStoreMapping>()
                .AnyAsync(pms =>
                    pms.ProductId == productId && pms.MerchantStoreId == merchantStoreId
                );
        }

        public async Task<
            List<(int MerchantStoreId, int Deducted)>
        > DeductProductQuantityForDeliveryAsync(int productId, int quantity)
        {
            var mappings = await _db.Set<ProductMerchantStoreMapping>()
                .Where(x => x.ProductId == productId && x.Quantity > 0)
                .OrderBy(x => x.DeliveryPriority)
                .ToListAsync();

            var remaining = quantity;
            var breakdown = new List<(int, int)>();

            foreach (var mapping in mappings)
            {
                if (remaining <= 0)
                    break;

                var deduct = Math.Min(remaining, mapping.Quantity);
                mapping.Quantity -= deduct;
                remaining -= deduct;

                breakdown.Add((mapping.MerchantStoreId, deduct));
            }

            if (remaining > 0)
                return null;

            await _db.SaveChangesAsync();
            return breakdown;
        }
    }
}

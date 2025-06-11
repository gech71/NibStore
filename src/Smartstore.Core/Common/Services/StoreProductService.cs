
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
            Console.WriteLine("boom");
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

      public async Task<bool> AdjustStoreInventoryAsync(int productId, int? storeId, int quantityToReduce)
{
            Console.WriteLine("BOOM");
    try
    {
        Console.WriteLine($"[DEBUG] Entered AdjustStoreInventoryAsync: ProductId={productId}, StoreId={storeId}, QuantityToReduce={quantityToReduce}");

        if (storeId == null)
        {
            throw new ArgumentNullException(nameof(storeId), $"StoreId is null.");
        }

        if (quantityToReduce <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantityToReduce), $"QuantityToReduce must be > 0.");
        }

        if (_db == null)
        {
            throw new NullReferenceException("DbContext (_db) is null.");
        }

        Console.WriteLine("[DEBUG] Getting DbSet<ProductMerchantStoreMapping>...");
        var dbSet = _db.Set<ProductMerchantStoreMapping>();
        if (dbSet == null)
        {
            throw new NullReferenceException("DbSet<ProductMerchantStoreMapping> is null.");
        }

        Console.WriteLine("[DEBUG] Querying for ProductMerchantStoreMapping...");
        var mapping = await dbSet.FirstOrDefaultAsync(m =>
            m.ProductId == productId &&
            m.MerchantStoreId == storeId
        );

        if (mapping == null)
        {
            throw new InvalidOperationException($"No ProductMerchantStoreMapping found.");
        }

        Console.WriteLine($"[DEBUG] Mapping found: ProductId={mapping.ProductId}, StoreId={mapping.MerchantStoreId}, Qty={mapping.Quantity}");

        if (mapping.Quantity < quantityToReduce)
        {
            throw new InvalidOperationException($"Insufficient stock: Available={mapping.Quantity}, Requested={quantityToReduce}");
        }

        Console.WriteLine($"[DEBUG] Reducing quantity...");
        mapping.Quantity -= quantityToReduce;

        Console.WriteLine("[DEBUG] Saving changes...");
        await _db.SaveChangesAsync();

        Console.WriteLine("[DEBUG] Adjustment successful.");
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[EXCEPTION] AdjustStoreInventoryAsync: {ex.GetType()} | {ex.Message}");
        Console.WriteLine(ex.StackTrace);
        throw new Exception(
            $"Failed to adjust inventory for ProductId={productId}, StoreId={storeId}, Qty={quantityToReduce}: {ex.Message}",
            ex
        );
    }
}


    }
}
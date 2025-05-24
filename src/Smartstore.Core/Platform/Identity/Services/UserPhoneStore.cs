using Smartstore.Core.Data;
using Smartstore.Core.Identity;

namespace Smartstore.Core.Platform.Identity.Services;

public class UserPhoneStore : IUserPhoneStore
{
    private readonly SmartDbContext _db;

    public UserPhoneStore(SmartDbContext db)
    {
        _db = db;
    }

    public async Task<Customer> FindByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();


        return await _db.GenericAttributes
            .Where(ga => ga.Key == "Phone" && ga.Value == phoneNumber)
            .Join(
                _db.Customers,
                ga => ga.EntityId,
                c => c.Id,
                (ga, c) => c
            )
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
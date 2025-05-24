using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using Smartstore.Core.Identity;

namespace Smartstore.Core.Platform.Identity.Services
{
    public interface IUserPhoneStore
    {
        Task<Customer> FindByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken);
    }
}
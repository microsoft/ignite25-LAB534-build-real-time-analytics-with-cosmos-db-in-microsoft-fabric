using System.Threading;
using FourthCoffee.Blazor.Models;

namespace FourthCoffee.Blazor.Interfaces
{
    public interface ICustomerService
    {
        Task<List<Customer>> GetCustomersAsync(int maxCount = 25, CancellationToken cancellationToken = default);
        Task<List<Customer>> SearchCustomersAsync(string searchText, int maxResults = 25, CancellationToken cancellationToken = default);
        Task<List<Customer>> GetRandomCustomersAsync(int count = 5);
        Task<Customer?> GetCustomerByIdAsync(string customerId);
        Task<List<Customer>> GetAllCustomersAsync();
    }
}

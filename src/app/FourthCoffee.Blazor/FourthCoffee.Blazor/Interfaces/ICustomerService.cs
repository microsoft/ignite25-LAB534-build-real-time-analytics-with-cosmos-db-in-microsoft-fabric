using FourthCoffee.Blazor.Models;

namespace FourthCoffee.Blazor.Interfaces
{
    public interface ICustomerService
    {
        Task<List<Customer>> GetRandomCustomersAsync(int count = 5);
        Task<Customer?> GetCustomerByIdAsync(string customerId);
        Task<List<Customer>> GetAllCustomersAsync();
    }
}

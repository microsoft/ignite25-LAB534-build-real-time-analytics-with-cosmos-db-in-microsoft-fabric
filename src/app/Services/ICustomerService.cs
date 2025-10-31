using CustomerDemoApp.Models;

namespace CustomerDemoApp.Services;

public interface ICustomerService
{
    Task<List<Customer>> GetRandomCustomersAsync(int count = 5);
    Task<Customer?> GetCustomerByIdAsync(string customerId);
    Task<List<Customer>> GetAllCustomersAsync();
}
using FourthCoffee.Blazor.Interfaces;
using FourthCoffee.Blazor.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace FourthCoffee.Blazor.Services
{
    public class LocalJsonCustomerService : ICustomerService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly string _jsonFilePath = "data/customers.json";
        private List<Customer>? _customers;
        private readonly ILogger<LocalJsonCustomerService> _logger;

        public LocalJsonCustomerService(IWebHostEnvironment environment, ILogger<LocalJsonCustomerService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Customer model is preserved")]
        [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Customer model is preserved")]
        private async Task<List<Customer>> LoadCustomersAsync()
        {
            if (_customers != null)
                return _customers;

            try
            {
                _logger.LogInformation("🔄 Loading customer data from local JSON file...");

                // Load from file system (server-side)
                var fullPath = Path.Combine(_environment.WebRootPath, _jsonFilePath);

                if (File.Exists(fullPath))
                {
                    var jsonContent = await File.ReadAllTextAsync(fullPath);
                    _customers = JsonSerializer.Deserialize<List<Customer>>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<Customer>();

                    _logger.LogInformation("✅ Successfully loaded {CustomerCount} customers from {JsonFile}", _customers.Count, _jsonFilePath);
                }
                else
                {
                    _logger.LogWarning("❌ Could not find file: {FullPath}. Ensure the JSON file exists in wwwroot/data/customers.json.", fullPath);
                    _customers = new List<Customer>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading customers from JSON file. Check that the file exists and has valid JSON.");
                _customers = new List<Customer>();
            }

            return _customers;
        }

        public async Task<List<Customer>> GetRandomCustomersAsync(int count = 5)
        {
            var customers = await LoadCustomersAsync();

            if (!customers.Any())
                return new List<Customer>();

            // Get random customers
            var random = new Random();
            return customers.OrderBy(x => random.Next()).Take(count).ToList();
        }

        public async Task<Customer?> GetCustomerByIdAsync(string customerId)
        {
            var customers = await LoadCustomersAsync();
            return customers.FirstOrDefault(c => c.Id == customerId || c.CustomerId == customerId);
        }

        public async Task<List<Customer>> GetAllCustomersAsync()
        {
            return await LoadCustomersAsync();
        }
    }
}

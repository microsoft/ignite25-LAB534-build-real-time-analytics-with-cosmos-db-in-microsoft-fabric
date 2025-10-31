using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FourthCoffee.Blazor.Interfaces;
using FourthCoffee.Blazor.Models;
using Microsoft.Extensions.Logging;

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

        public async Task<List<Customer>> GetCustomersAsync(int maxCount = 25, CancellationToken cancellationToken = default)
        {
            if (maxCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount), "The maximum number of customers must be greater than zero.");
            }

            var customers = await LoadCustomersAsync();

            return customers
                .Where(c => c.Recommendations?.Any() == true)
                .OrderByDescending(c => c.LastPurchaseDate ?? DateTime.MinValue)
                .ThenBy(c => c.Name ?? string.Empty)
                .Take(maxCount)
                .ToList();
        }

        public async Task<List<Customer>> SearchCustomersAsync(string searchText, int maxResults = 25, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return new List<Customer>();
            }

            if (maxResults <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxResults), "The maximum number of customers must be greater than zero.");
            }

            var comparison = StringComparison.CurrentCultureIgnoreCase;
            var normalizedSearch = searchText.Trim();
            var customers = await LoadCustomersAsync();

            return customers
                .Where(c => c.Recommendations?.Any() == true)
                .Where(c => (!string.IsNullOrWhiteSpace(c.Name) && c.Name.Contains(normalizedSearch, comparison)) ||
                            (!string.IsNullOrWhiteSpace(c.Email) && c.Email.Contains(normalizedSearch, comparison)))
                .OrderByDescending(c => c.LastPurchaseDate ?? DateTime.MinValue)
                .ThenBy(c => c.Name ?? string.Empty)
                .Take(maxResults)
                .ToList();
        }

        public async Task<List<Customer>> GetRandomCustomersAsync(int count = 5)
        {
            var seedCount = Math.Max(count * 4, count);
            var seededCustomers = await GetCustomersAsync(seedCount, CancellationToken.None);

            if (seededCustomers.Count <= count)
            {
                return seededCustomers;
            }

            var random = new Random();
            return seededCustomers
                .OrderBy(_ => random.Next())
                .Take(count)
                .ToList();
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

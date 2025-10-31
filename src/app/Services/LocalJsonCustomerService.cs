using CustomerDemoApp.Models;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;

namespace CustomerDemoApp.Services;

public class LocalJsonCustomerService : ICustomerService
{
    private readonly IWebHostEnvironment _environment;
    private readonly string _jsonFilePath = "data/customers.json";
    private List<Customer>? _customers;

    public LocalJsonCustomerService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Customer model is preserved")]
    [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Customer model is preserved")]
    private async Task<List<Customer>> LoadCustomersAsync()
    {
        if (_customers != null)
            return _customers;

        try
        {
            Console.WriteLine("🔄 Loading customer data from local JSON file...");
            
            // Load from file system (server-side)
            var fullPath = Path.Combine(_environment.WebRootPath, _jsonFilePath);
            
            if (File.Exists(fullPath))
            {
                var jsonContent = await File.ReadAllTextAsync(fullPath);
                _customers = JsonSerializer.Deserialize<List<Customer>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<Customer>();
                
                Console.WriteLine($"✅ Successfully loaded {_customers.Count} customers from {_jsonFilePath}");
            }
            else
            {
                Console.WriteLine($"❌ Could not find file: {fullPath}");
                Console.WriteLine("💡 Make sure the JSON file exists in wwwroot/data/customers.json");
                _customers = new List<Customer>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading customers from JSON file: {ex.Message}");
            Console.WriteLine("💡 Check if the JSON file exists and has valid format");
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
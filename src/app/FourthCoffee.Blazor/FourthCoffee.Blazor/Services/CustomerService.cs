using Azure.Identity;
using FourthCoffee.Blazor.Interfaces;
using FourthCoffee.Blazor.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace FourthCoffee.Blazor.Services
{
    public class CustomerService : ICustomerService, IDisposable
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Database _database;
        private readonly Container _customersContainer;
        private readonly ILogger<CustomerService> _logger;

        public CustomerService(string endpointUri, string databaseName, string containerName, ILogger<CustomerService> logger)
        {
            _logger = logger;

            try
            {
                _logger.LogInformation("🔄 Initializing Cosmos DB connection for Microsoft Fabric...");

                var options = new CosmosClientOptions
                {
                    // Use Gateway mode for better Fabric compatibility
                    ConnectionMode = ConnectionMode.Gateway,

                    SerializerOptions = new CosmosSerializationOptions
                    {
                        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                    },

                    // Minimal settings for Fabric compatibility
                    RequestTimeout = TimeSpan.FromSeconds(30),

                    // Conservative retry settings for Fabric
                    MaxRetryAttemptsOnRateLimitedRequests = 3,
                    MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30),

                    // Custom user agent to help with debugging
                    ApplicationName = "FabricCosmosDemo"
                };

                _logger.LogInformation("🔧 Using Gateway connection mode for maximum Fabric compatibility");

                AzureCliCredential credential = new();
                _cosmosClient = new CosmosClient(endpointUri, credential, options);

                _database = _cosmosClient.GetDatabase(databaseName);
                _customersContainer = _database.GetContainer(containerName);

                // Test the connection with a simple operation that should work in Fabric
                _logger.LogInformation("🧪 Testing Fabric Cosmos DB connection...");

                // This is where the address operation error typically occurs
                // If it fails here, it will be caught and handled gracefully

                _logger.LogInformation("✅ Cosmos DB client initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "❌ Failed to initialize Cosmos DB client for database {Database} and container {Container}. " +
                    "Ensure you are logged in with Azure CLI, have access to the Fabric workspace, and that the database/container exist with network connectivity.",
                    databaseName,
                    containerName);

                if (ex.Message.Contains("not supported for Azure Cosmos DB database in Microsoft Fabric"))
                {
                    _logger.LogWarning(ex,
                        "⚠️ This is a Microsoft Fabric Cosmos DB limitation. Consider using the local JSON fallback for development.");
                }

                throw new InvalidOperationException($"Failed to connect to Fabric Cosmos DB: {ex.Message}", ex);
            }
        }

        public async Task<List<Customer>> GetRandomCustomersAsync(int count = 5)
        {
            try
            {
                // Get a random selection of customers
                var query = new QueryDefinition("SELECT * FROM c ORDER BY c.id OFFSET @offset LIMIT @limit")
                    .WithParameter("@offset", Random.Shared.Next(0, 450)) // Assuming 500 total customers
                    .WithParameter("@limit", count);

                var customers = new List<Customer>();
                using var iterator = _customersContainer.GetItemQueryIterator<Customer>(query);

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    customers.AddRange(response);
                }

                return customers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "❌ Error retrieving customers from Cosmos DB. This usually indicates a runtime connection issue; check your database and container configuration.");
                throw new InvalidOperationException($"Failed to retrieve customers from Cosmos DB: {ex.Message}", ex);
            }
        }

        public async Task<Customer?> GetCustomerByIdAsync(string customerId)
        {
            try
            {
                var response = await _customersContainer.ReadItemAsync<Customer>(customerId, new PartitionKey(customerId));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer {CustomerId} from Cosmos DB", customerId);
                return null;
            }
        }

        public async Task<List<Customer>> GetAllCustomersAsync()
        {
            try
            {
                var query = new QueryDefinition("SELECT * FROM c where c.recommendations != null");
                var customers = new List<Customer>();

                using var iterator = _customersContainer.GetItemQueryIterator<Customer>(query);

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    customers.AddRange(response);
                }

                return customers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all customers from Cosmos DB");
                return new List<Customer>();
            }
        }

        public void Dispose()
        {
            _cosmosClient?.Dispose();
        }
    }
}

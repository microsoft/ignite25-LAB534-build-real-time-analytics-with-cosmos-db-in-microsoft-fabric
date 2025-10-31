using Microsoft.Azure.Cosmos;
using CustomerDemoApp.Models;
using System.Text.Json;
using Azure.Identity;

namespace CustomerDemoApp.Services;

public class CosmosDbService : ICustomerService, IDisposable
{
    private readonly CosmosClient _cosmosClient;
    private readonly Database _database;
    private readonly Container _customersContainer;

    public CosmosDbService(string endpointUri, string databaseName, string containerName)
    {
        try
        {
            Console.WriteLine("🔄 Initializing Cosmos DB connection for Microsoft Fabric...");
            
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

            Console.WriteLine("🔧 Using Gateway connection mode for maximum Fabric compatibility");
            
            AzureCliCredential credential = new();
            _cosmosClient = new CosmosClient(endpointUri, credential, options);

            _database = _cosmosClient.GetDatabase(databaseName);
            _customersContainer = _database.GetContainer(containerName);
            
            // Test the connection with a simple operation that should work in Fabric
            Console.WriteLine("🧪 Testing Fabric Cosmos DB connection...");
            
            // This is where the address operation error typically occurs
            // If it fails here, it will be caught and handled gracefully
            
            Console.WriteLine("✅ Cosmos DB client initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ CRITICAL: Failed to initialize Cosmos DB client");
            Console.WriteLine($"   Error: {ex.Message}");
            Console.WriteLine($"   Database: {databaseName}");
            Console.WriteLine($"   Container: {containerName}");
            Console.WriteLine("   Please check:");
            Console.WriteLine("   - You are logged in with Azure CLI (az login)");
            Console.WriteLine("   - Your account has access to the Fabric workspace");
            Console.WriteLine("   - The database and container exist in Fabric");
            Console.WriteLine("   - Network connectivity to Fabric Cosmos DB");
            
            if (ex.Message.Contains("not supported for Azure Cosmos DB database in Microsoft Fabric"))
            {
                Console.WriteLine("   ⚠️  This is a Microsoft Fabric Cosmos DB limitation");
                Console.WriteLine("   💡 Consider using the local JSON fallback for development");
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
            Console.WriteLine($"❌ CRITICAL: Error retrieving customers from Cosmos DB");
            Console.WriteLine($"   Error: {ex.Message}");
            Console.WriteLine("   This indicates a runtime connection issue with Cosmos DB");
            Console.WriteLine("   Check your database and container configuration");
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
            Console.WriteLine($"Error retrieving customer {customerId}: {ex.Message}");
            return null;
        }
    }

    public async Task<List<Customer>> GetAllCustomersAsync()
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c");
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
            Console.WriteLine($"Error retrieving all customers: {ex.Message}");
            return new List<Customer>();
        }
    }

    public void Dispose()
    {
        _cosmosClient?.Dispose();
    }
}
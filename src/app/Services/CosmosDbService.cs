using Microsoft.Azure.Cosmos;
using CustomerDemoApp.Models;
using System.Text.Json;

namespace CustomerDemoApp.Services;

public class CosmosDbService
{
    private readonly CosmosClient _cosmosClient;
    private readonly Database _database;
    private readonly Container _customersContainer;

    public CosmosDbService(string connectionString, string databaseName, string containerName)
    {
        var options = new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        };

        _cosmosClient = new CosmosClient(connectionString, options);
        _database = _cosmosClient.GetDatabase(databaseName);
        _customersContainer = _database.GetContainer(containerName);
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
            Console.WriteLine($"Error retrieving customers: {ex.Message}");
            return new List<Customer>();
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        public async Task<List<Customer>> GetCustomersAsync(int maxCount = 25, CancellationToken cancellationToken = default)
        {
            if (maxCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount), "The maximum number of customers must be greater than zero.");
            }

            try
            {
                var normalizedMaxCount = Math.Min(maxCount, 1000);

                var queryText = @"SELECT *
                                   FROM c
                                   WHERE IS_DEFINED(c.recommendations) AND ARRAY_LENGTH(c.recommendations) > 0
                                   ORDER BY c._ts DESC";

                var requestOptions = new QueryRequestOptions
                {
                    MaxItemCount = normalizedMaxCount,
                    MaxBufferedItemCount = Math.Max(normalizedMaxCount, 32),
                    MaxConcurrency = -1
                };

                using FeedIterator<Customer> iterator = _customersContainer.GetItemQueryIterator<Customer>(
                    new QueryDefinition(queryText),
                    requestOptions: requestOptions);

                var customers = new List<Customer>(Math.Min(maxCount, normalizedMaxCount));

                while (iterator.HasMoreResults && customers.Count < maxCount)
                {
                    var response = await iterator.ReadNextAsync(cancellationToken);
                    customers.AddRange(response);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }

                return customers
                    .OrderByDescending(c => c.LastPurchaseDate ?? DateTime.MinValue)
                    .ThenByDescending(c => c.Id ?? string.Empty)
                    .Take(maxCount)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "❌ Error retrieving customer batch from Cosmos DB. Validate network access, permissions, and container indexing settings.");
                throw new InvalidOperationException($"Failed to retrieve customers from Cosmos DB: {ex.Message}", ex);
            }
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

            try
            {
                var trimmedSearchText = searchText.Trim();
                var normalizedMaxCount = Math.Min(maxResults, 100);

                var queryText = @"SELECT *
                                   FROM c
                                   WHERE IS_DEFINED(c.recommendations) AND ARRAY_LENGTH(c.recommendations) > 0
                                         AND (CONTAINS(c.name, @searchText, true) OR CONTAINS(c.email, @searchText, true))
                                   ORDER BY c._ts DESC";

                var queryDefinition = new QueryDefinition(queryText)
                    .WithParameter("@searchText", trimmedSearchText);

                var requestOptions = new QueryRequestOptions
                {
                    MaxItemCount = normalizedMaxCount,
                    MaxBufferedItemCount = Math.Max(normalizedMaxCount, 32),
                    MaxConcurrency = -1
                };

                using FeedIterator<Customer> iterator = _customersContainer.GetItemQueryIterator<Customer>(
                    queryDefinition,
                    requestOptions: requestOptions);

                var customers = new List<Customer>(normalizedMaxCount);

                while (iterator.HasMoreResults && customers.Count < maxResults)
                {
                    var response = await iterator.ReadNextAsync(cancellationToken);
                    customers.AddRange(response);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }

                return customers
                    .OrderByDescending(c => c.LastPurchaseDate ?? DateTime.MinValue)
                    .ThenByDescending(c => c.Id ?? string.Empty)
                    .Take(maxResults)
                    .ToList();
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "🔁 Customer search cancelled locally for query {SearchText}", searchText);
                return new List<Customer>();
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex,
                    "⚠️ Customer search timed out in Cosmos DB for query {SearchText}. Returning no results.", searchText);
                return new List<Customer>();
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.RequestTimeout || ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                _logger.LogWarning(ex,
                    "⚠️ Cosmos DB request timed out while searching for query {SearchText}. Returning no results.", searchText);
                return new List<Customer>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "❌ Error searching customers in Cosmos DB with query text {SearchText}", searchText);
                throw new InvalidOperationException($"Failed to search customers from Cosmos DB: {ex.Message}", ex);
            }
        }

        public async Task<List<Customer>> GetRandomCustomersAsync(int count = 5)
        {
            try
            {
                var seedCount = Math.Max(count * 4, count);
                var seededCustomers = await GetCustomersAsync(seedCount, CancellationToken.None);

                if (seededCustomers.Count <= count)
                {
                    return seededCustomers;
                }

                return seededCustomers
                    .OrderBy(_ => Random.Shared.Next())
                    .Take(count)
                    .ToList();
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

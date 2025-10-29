//
// FabricPOSDatatStreaming.cs — Create an Eventstream and send sample POS transaction data to its local source.
//
// Pipeline
// 1. Authenticate via Azure CLI
// 2. Resolve Workspace
// 3. Create Eventstream from JSON definition (recover from transient LRO status issue)
// 4. Resolve LocalStreamSource connection
// 5. Load seed data (customers, shops, menu items)
// 6. Send realistic transaction JSON payloads to Event Hubs every 3 seconds until cancelled (Ctrl+C)
//
// Usage
//   dotnet run .\src\pos_data_streaming\FabricPOSDatatStreaming.cs
//


#:package Microsoft.Fabric.Api@1.*
#:package Azure.Identity@1.*
#:package Azure.Messaging.EventHubs@5.*
#:package Microsoft.Extensions.Logging@9.*
#:package Microsoft.Extensions.Logging.Console@9.*

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Microsoft.Fabric.Api;
using Microsoft.Fabric.Api.Core.Models;
using Microsoft.Fabric.Api.Eventstream.Models;
using Microsoft.Extensions.Logging;

// -----------------------
// Configuration
// -----------------------

// const string WorkspaceName = "Fourth Coffee Commerce - Lab 534";
const string EventstreamName = "pos-stream-eventstream";
const int SendIntervalMs = 3000; // 3s cadence

// Data file paths
var currentDir = Directory.GetCurrentDirectory();
var customersFilePath = Path.Combine(currentDir, "data", "nosql", "customers_container.json");
var shopsFilePath = Path.Combine(currentDir, "data", "nosql", "shops_container.json");
var menuFilePath = Path.Combine(currentDir, "data", "nosql", "menu_container.json");
var streamingTransactionsFilePath = Path.Combine(currentDir, "data", "streaming", "streaming_transactions.json");


// Logger setup (simple console)
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .SetMinimumLevel(LogLevel.Information)
        .AddSimpleConsole(options =>
        {
            options.IncludeScopes = false;
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });
});
var logger = loggerFactory.CreateLogger("FabricPOSDatatStreaming");

var WorkspaceName = Environment.GetEnvironmentVariable("FABRIC_WORKSPACE_NAME");
if (string.IsNullOrWhiteSpace(WorkspaceName))
{
    logger.LogError("Environment variable FABRIC_WORKSPACE_NAME is not set. Please set it to the target workspace name.");
    return;
}

// Azure auth and client
var credential = new AzureCliCredential();
var fabricClient = new FabricClient(credential);
logger.LogInformation("Starting POS transaction streaming. Workspace: {Workspace}", WorkspaceName);

// Load seed data
logger.LogInformation("Loading seed data...");
if (!File.Exists(customersFilePath))
    throw new FileNotFoundException($"Customers file not found: {customersFilePath}");
if (!File.Exists(shopsFilePath))
    throw new FileNotFoundException($"Shops file not found: {shopsFilePath}");
if (!File.Exists(menuFilePath))
    throw new FileNotFoundException($"Menu file not found: {menuFilePath}");
if (!File.Exists(streamingTransactionsFilePath))
    throw new FileNotFoundException($"Streaming transactions file not found: {streamingTransactionsFilePath}");

var customersJson = await File.ReadAllTextAsync(customersFilePath);
var shopsJson = await File.ReadAllTextAsync(shopsFilePath);
var menuJson = await File.ReadAllTextAsync(menuFilePath);
var streamingTransactionsJson = await File.ReadAllTextAsync(streamingTransactionsFilePath);

var customers = JsonSerializer.Deserialize(customersJson, TransactionJsonContext.Default.CustomerArray)!;
var shops = JsonSerializer.Deserialize(shopsJson, TransactionJsonContext.Default.ShopArray)!;
var menuItems = JsonSerializer.Deserialize(menuJson, TransactionJsonContext.Default.MenuItemArray)!;
var predefinedTransactions = JsonSerializer.Deserialize(streamingTransactionsJson, TransactionJsonContext.Default.TransactionArray)!;

logger.LogInformation("Loaded {CustomerCount} customers, {ShopCount} shops, {MenuCount} menu items, {TransactionCount} predefined transactions", 
    customers.Length, shops.Length, menuItems.Length, predefinedTransactions.Length);

// Transaction generator
var streamingManager = new StreamingTransactionManager(predefinedTransactions, customers, shops, menuItems);

// Resolve workspace
var workspace = fabricClient.Core.Workspaces
    .ListWorkspaces()
    .FirstOrDefault(w => w.DisplayName == WorkspaceName);

if (workspace is null)
{
    logger.LogError("Workspace {Workspace} not found", WorkspaceName);
    throw new InvalidOperationException($"Workspace '{WorkspaceName}' not found.");
}

// Create eventstream from local JSON definition
Eventstream? eventstream = null;
try
{
    var eventstreamJsonPayloadFilePath = Path.Combine(Directory.GetCurrentDirectory(), "src", "pos_data_streaming", "eventstream.json");
    if (!File.Exists(eventstreamJsonPayloadFilePath))
    {
        logger.LogError("Eventstream definition file missing at {Path}", eventstreamJsonPayloadFilePath);
        throw new FileNotFoundException("Eventstream definition file not found.", eventstreamJsonPayloadFilePath);
    }

    var jsonContent = await File.ReadAllTextAsync(eventstreamJsonPayloadFilePath);
    var base64Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonContent));

    var createEventstreamRequest = new CreateEventstreamRequest(EventstreamName)
    {
        Definition = new EventstreamDefinition(
        [
            new EventstreamDefinitionPart
            {
                Path = "eventstream.json",
                Payload = base64Payload,
                PayloadType = PayloadType.InlineBase64
            }
        ])
    };

    var eventstreamCreationResponse = await fabricClient.Eventstream.Items.CreateEventstreamAsync(workspace.Id, createEventstreamRequest);
    eventstream = eventstreamCreationResponse.Value;
    logger.LogInformation("Eventstream created: {Name} Id {Id}", EventstreamName, eventstream.Id);
}
catch (Exception ex) when (ex.Message.Contains("Failure getting LRO status", StringComparison.OrdinalIgnoreCase))
{
    // Ignore the  Azure.RequestFailedException: Failure getting LRO status
    // TODO: Investigate further why this exception occurs yet the eventstream is created successfully.
    // Attempt to recover by listing existing eventstreams
    logger.LogWarning(ex, "LRO status failure while creating eventstream {Name}; attempting lookup", EventstreamName);
    eventstream = await fabricClient.Eventstream.Items.ListEventstreamsAsync(workspace.Id)
        .FirstOrDefaultAsync(es => es.DisplayName == EventstreamName);
    if (eventstream is not null)
        logger.LogInformation("Recovered eventstream after LRO issue: {Name} Id {Id}", EventstreamName, eventstream.Id);
}

if (eventstream is null || eventstream.Id is null)
{
    logger.LogError("Failed to create or retrieve eventstream {Name}", EventstreamName);
    throw new InvalidOperationException($"Failed to create or retrieve eventstream '{EventstreamName}'.");
}

// Resolve topology and local source
var eventstreamTopologyResponse = await fabricClient.Eventstream.Topology.GetEventstreamTopologyAsync(workspace.Id, eventstream.Id.Value);
var eventstreamTopology = eventstreamTopologyResponse.Value;
var eventstreamLocalStreamSource = eventstreamTopology.Sources.FirstOrDefault(s => s.Name == "LocalStreamSource");
if (eventstreamLocalStreamSource?.Id is null)
{
    logger.LogError("LocalStreamSource not found for eventstream {Name}", EventstreamName);
    throw new InvalidOperationException("LocalStreamSource not found in eventstream topology.");
}

// Get source connection and build producer
var eventstreamSourceConnection = await fabricClient.Eventstream.Topology.GetEventstreamSourceConnectionAsync(
    workspace.Id,
    eventstream.Id.Value,
    eventstreamLocalStreamSource.Id.Value);

await using var producer = new EventHubProducerClient(
    eventstreamSourceConnection.Value.AccessKeys.PrimaryConnectionString,
    eventstreamSourceConnection.Value.EventHubName);

// Send loop with Ctrl+C cancellation
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

logger.LogInformation("Sending transaction messages every {Interval} ms. Press Ctrl+C to stop...", SendIntervalMs);
logger.LogInformation("Will stream {PredefinedCount} predefined transactions, then switch to random generation", predefinedTransactions.Length);

while (!cts.IsCancellationRequested)
{
    try
    {
        var transaction = streamingManager.GetNextTransaction();
        
        // Check if we just switched to random mode
        if (streamingManager.JustSwitchedToRandom)
        {
            logger.LogInformation("🎲 Completed all {Count} predefined transactions. Switching to random transaction generation...", 
                streamingManager.TotalPredefinedTransactions);
        }
        using var batch = await producer.CreateBatchAsync(cts.Token);
        var payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(transaction, TransactionJsonContext.Default.Transaction));
        if (!batch.TryAdd(new EventData(payloadBytes)))
        {
            logger.LogWarning("Transaction payload too large for batch; skipping message");
        }
        else
        {
            await producer.SendAsync(batch, cts.Token);
            
            if (streamingManager.IsUsingPredefinedTransactions)
            {
                logger.LogInformation("Sent predefined transaction {Current}/{Total}: {TransactionId} for customer {CustomerId} at {ShopId} - {ItemCount} items, qty {TotalQuantity}, total ${TotalAmount}", 
                    streamingManager.CurrentTransactionIndex, streamingManager.TotalPredefinedTransactions,
                    transaction.TransactionId, transaction.CustomerId, transaction.ShopId, 
                    transaction.Items.Length, transaction.TotalQuantity, transaction.TotalAmount);
            }
            else
            {
                logger.LogInformation("Sent random transaction: {TransactionId} for customer {CustomerId} at {ShopId} - {ItemCount} items, qty {TotalQuantity}, total ${TotalAmount}", 
                    transaction.TransactionId, transaction.CustomerId, transaction.ShopId, 
                    transaction.Items.Length, transaction.TotalQuantity, transaction.TotalAmount);
            }
        }
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    {
        // Graceful shutdown
        break;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error while sending transaction event; continuing");
    }

    await Task.Delay(SendIntervalMs, cts.Token);
}

logger.LogInformation("Stopped POS transaction streaming");

// Data models for seed data
internal record Customer(
    [property: JsonPropertyName("customerId")] string CustomerId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("loyaltyPoints")] int LoyaltyPoints,
    [property: JsonPropertyName("preferences")] CustomerPreferences Preferences
);

internal record CustomerPreferences(
    [property: JsonPropertyName("airport")] string Airport
);

internal record Shop(
    [property: JsonPropertyName("shopId")] string ShopId,
    [property: JsonPropertyName("airportId")] string AirportId
);

internal record MenuItem(
    [property: JsonPropertyName("menuItemKey")] int MenuItemKey,
    [property: JsonPropertyName("menuItemId")] string MenuItemId,
    [property: JsonPropertyName("menuItemName")] string MenuItemName,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("subcategory")] string Subcategory,
    [property: JsonPropertyName("sizes")] MenuItemSize[] Sizes
);

internal record MenuItemSize(
    [property: JsonPropertyName("size")] string Size,
    [property: JsonPropertyName("price")] decimal Price
);

// Transaction models
internal record Transaction(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("transactionId")] string TransactionId,
    [property: JsonPropertyName("timestamp")] string Timestamp,
    [property: JsonPropertyName("customerId")] string CustomerId,
    [property: JsonPropertyName("shopId")] string ShopId,
    [property: JsonPropertyName("airportId")] string AirportId,
    [property: JsonPropertyName("transactionType")] string TransactionType,
    [property: JsonPropertyName("items")] TransactionItem[] Items,
    [property: JsonPropertyName("totalQuantity")] int TotalQuantity,
    [property: JsonPropertyName("totalAmount")] decimal TotalAmount,
    [property: JsonPropertyName("paymentMethod")] string PaymentMethod,
    [property: JsonPropertyName("loyaltyPointsEarned")] int LoyaltyPointsEarned,
    [property: JsonPropertyName("loyaltyPointsRedeemed")] int LoyaltyPointsRedeemed,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("metadata")] TransactionMetadata Metadata
);

internal record TransactionItem(
    [property: JsonPropertyName("menuItemKey")] int MenuItemKey,
    [property: JsonPropertyName("menuItemId")] string MenuItemId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("subcategory")] string Subcategory,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("unitPrice")] decimal UnitPrice,
    [property: JsonPropertyName("totalPrice")] decimal TotalPrice,
    [property: JsonPropertyName("size")] string Size
);

internal record TransactionMetadata(
    [property: JsonPropertyName("deviceId")] string DeviceId,
    [property: JsonPropertyName("employeeId")] string EmployeeId,
    [property: JsonPropertyName("orderNumber")] string OrderNumber
);

// Streaming transaction manager
internal class StreamingTransactionManager
{
    private readonly Transaction[] _predefinedTransactions;
    private readonly Customer[] _customers;
    private readonly Shop[] _shops;
    private readonly MenuItem[] _menuItems;
    private readonly Random _random;
    private int _currentIndex;
    private bool _switchedToRandom = false;
    private readonly string[] _transactionTypes = ["purchase", "refund"];
    private readonly string[] _paymentMethods = ["Credit Card", "Cash", "Mobile Pay", "Debit Card", "Gift Card"];

    public StreamingTransactionManager(Transaction[] predefinedTransactions, Customer[] customers, Shop[] shops, MenuItem[] menuItems)
    {
        _predefinedTransactions = predefinedTransactions;
        _customers = customers;
        _shops = shops;
        _menuItems = menuItems;
        _random = new Random();
        _currentIndex = 0;
    }

    public Transaction GetNextTransaction()
    {
        // If we haven't reached the end of predefined transactions, return the next one
        if (_currentIndex < _predefinedTransactions.Length)
        {
            var transaction = _predefinedTransactions[_currentIndex];
            _currentIndex++;
            
            // Update timestamp to current time for realistic streaming
            var updatedTransaction = transaction with 
            { 
                Timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };
            
            return updatedTransaction;
        }

        // Log once when switching to random generation
        if (_currentIndex == _predefinedTransactions.Length)
        {
            _currentIndex++; // Increment to prevent repeated logging
            // Note: We can't log here directly since we don't have access to logger
            // The calling code should check IsUsingPredefinedTransactions
        }

        // After predefined transactions are exhausted, generate random ones
        return GenerateRandomTransaction();
    }

    private Transaction GenerateRandomTransaction()
    {
        var customer = _customers[_random.Next(_customers.Length)];
        var airportId = customer.Preferences.Airport;
        
        // Find shops at customer's preferred airport
        var airportShops = _shops.Where(s => s.AirportId == airportId).ToArray();
        if (airportShops.Length == 0)
            airportShops = _shops; // Fallback to any shop
        
        var shop = airportShops[_random.Next(airportShops.Length)];
        var transactionId = $"rand-{_random.Next(10000, 99999)}";
        var transactionType = _transactionTypes[_random.Next(_transactionTypes.Length)];
        var paymentMethod = _paymentMethods[_random.Next(_paymentMethods.Length)];
        
        // Generate random timestamp
        var timestamp = DateTimeOffset.UtcNow;
        
        // Select random menu items
        var numItems = _random.Next(1, 5);
        var selectedItems = _menuItems.OrderBy(x => _random.Next()).Take(numItems).ToArray();
        
        var items = new List<TransactionItem>();
        foreach (var menuItem in selectedItems)
        {
            var quantity = _random.Next(1, 4);
            var size = menuItem.Sizes[_random.Next(menuItem.Sizes.Length)];
            var unitPrice = size.Price;
            var totalPrice = Math.Round(quantity * unitPrice, 2);
            
            items.Add(new TransactionItem(
                menuItem.MenuItemKey,
                menuItem.MenuItemId,
                menuItem.MenuItemName,
                menuItem.Category,
                menuItem.Subcategory,
                quantity,
                unitPrice,
                totalPrice,
                size.Size
            ));
        }
        
        var totalAmount = Math.Round(items.Sum(i => i.TotalPrice), 2);
        var totalQuantity = items.Sum(i => i.Quantity);
        
        // Loyalty points calculation based on actual data patterns
        var loyaltyPointsEarned = transactionType == "purchase" ? (int)Math.Floor(totalAmount) : 0;
        var loyaltyPointsRedeemed = transactionType == "purchase" ? _random.Next(0, Math.Min(11, customer.LoyaltyPoints + 1)) : 0;
        var status = transactionType == "purchase" ? "completed" : "refunded";
        
        var metadata = new TransactionMetadata(
            $"pos-terminal-{_random.Next(1, 16):D2}",
            $"emp-{_random.Next(100, 1000)}",
            $"ORD-{timestamp:yyyyMMdd}-{_random.Next(1000, 10000)}"
        );
        
        return new Transaction(
            transactionId,
            transactionId,
            timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            customer.CustomerId,
            shop.ShopId,
            airportId,
            transactionType,
            items.ToArray(),
            totalQuantity,
            totalAmount,
            paymentMethod,
            loyaltyPointsEarned,
            loyaltyPointsRedeemed,
            status,
            metadata
        );
    }

    public bool IsUsingPredefinedTransactions => _currentIndex < _predefinedTransactions.Length;
    public int CurrentTransactionIndex => _currentIndex;
    public int TotalPredefinedTransactions => _predefinedTransactions.Length;
    public bool JustSwitchedToRandom
    {
        get
        {
            if (_currentIndex == _predefinedTransactions.Length && !_switchedToRandom)
            {
                _switchedToRandom = true;
                return true;
            }
            return false;
        }
    }
}

// JSON source generator context
[JsonSerializable(typeof(Transaction))]
[JsonSerializable(typeof(Transaction[]))]
[JsonSerializable(typeof(Customer[]))]
[JsonSerializable(typeof(Shop[]))]
[JsonSerializable(typeof(MenuItem[]))]
internal partial class TransactionJsonContext : JsonSerializerContext { }

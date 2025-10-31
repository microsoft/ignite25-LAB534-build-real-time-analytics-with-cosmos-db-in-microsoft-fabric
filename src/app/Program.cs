using CustomerDemoApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Configure HttpClient for server-side Blazor
builder.Services.AddHttpClient();

// Configure customer service - now we can use Azure CLI credentials!
var cosmosEndpoint = "";
var databaseName = "fc_commerce_cosmos";
var containerName = "customers";

Console.WriteLine("=== Customer Data Service Configuration ===");

// Check if a valid Cosmos DB endpoint is provided
if (string.IsNullOrEmpty(cosmosEndpoint) || 
    cosmosEndpoint == "YOUR_COSMOS_DB_ENDPOINT_HERE" ||
    cosmosEndpoint.StartsWith("YOUR_"))
{
    // Use local JSON file service
    Console.WriteLine("🔄 FALLBACK MODE: Using local JSON file for customer data");
    Console.WriteLine("   Reason: Cosmos DB endpoint not configured");
    Console.WriteLine("   Data source: /wwwroot/data/customers.json");
    Console.WriteLine("   To use Cosmos DB: Update endpoint in Program.cs");
    builder.Services.AddScoped<ICustomerService, LocalJsonCustomerService>();
}
else
{
    // Use Cosmos DB service with Azure CLI credentials
    Console.WriteLine("🌐 COSMOS DB MODE: Attempting to connect to Cosmos DB");
    Console.WriteLine($"   Endpoint: {cosmosEndpoint}");
    Console.WriteLine($"   Database: {databaseName}");
    Console.WriteLine($"   Container: {containerName}");
    Console.WriteLine("   Authentication: Azure CLI Credential");
    Console.WriteLine("   Note: Will fall back to local JSON if Fabric limitations are encountered");
    
    // Register the service with automatic fallback for Fabric limitations
    builder.Services.AddScoped<ICustomerService>(sp => 
    {
        try
        {
            var cosmosService = new CosmosDbService(cosmosEndpoint, databaseName, containerName);
            Console.WriteLine("✅ Successfully connected to Cosmos DB");
            return cosmosService;
        }
        catch (Exception ex) when (ex.Message.Contains("not supported for Azure Cosmos DB database in Microsoft Fabric"))
        {
            Console.WriteLine("⚠️  Microsoft Fabric Cosmos DB limitation detected");
            Console.WriteLine("🔄 Automatically falling back to local JSON file");
            Console.WriteLine("💡 This is expected behavior for certain Fabric operations");
            return new LocalJsonCustomerService(sp.GetRequiredService<IWebHostEnvironment>());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to connect to Cosmos DB: {ex.Message}");
            Console.WriteLine("🔄 Falling back to local JSON file");
            return new LocalJsonCustomerService(sp.GetRequiredService<IWebHostEnvironment>());
        }
    });
}

Console.WriteLine("===========================================");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
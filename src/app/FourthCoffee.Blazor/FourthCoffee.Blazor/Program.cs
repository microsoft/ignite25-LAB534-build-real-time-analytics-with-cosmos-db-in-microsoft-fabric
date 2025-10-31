using FourthCoffee.Blazor.Components;
using FourthCoffee.Blazor.Interfaces;
using FourthCoffee.Blazor.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

var startupLogs = new List<(LogLevel Level, string Message)>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var config = builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>()
    .Build();

// Configure customer service - now we can use Azure CLI credentials!
var cosmosEndpoint = config["CosmosDb:Endpoint"];
var databaseName = "fc_commerce_cosmos";
var containerName = "customers";

startupLogs.Add((LogLevel.Information, "=== Customer Data Service Configuration ==="));

// Check if a valid Cosmos DB endpoint is provided
if (string.IsNullOrEmpty(cosmosEndpoint) ||
    cosmosEndpoint == "YOUR_COSMOS_DB_ENDPOINT_HERE" ||
    cosmosEndpoint.StartsWith("YOUR_"))
{
    // Use local JSON file service
    startupLogs.Add((LogLevel.Information, "🔄 FALLBACK MODE: Using local JSON file for customer data"));
    startupLogs.Add((LogLevel.Information, "   Reason: Cosmos DB endpoint not configured"));
    startupLogs.Add((LogLevel.Information, "   Data source: /wwwroot/data/customers.json"));
    startupLogs.Add((LogLevel.Information, "   To use Cosmos DB: Update endpoint in Program.cs"));
    builder.Services.AddScoped<ICustomerService, LocalJsonCustomerService>();
}
else
{
    // Use Cosmos DB service with Azure CLI credentials
    startupLogs.Add((LogLevel.Information, "🌐 COSMOS DB MODE: Attempting to connect to Cosmos DB"));
    startupLogs.Add((LogLevel.Information, $"   Endpoint: {cosmosEndpoint}"));
    startupLogs.Add((LogLevel.Information, $"   Database: {databaseName}"));
    startupLogs.Add((LogLevel.Information, $"   Container: {containerName}"));
    startupLogs.Add((LogLevel.Information, "   Authentication: Azure CLI Credential"));
    startupLogs.Add((LogLevel.Information, "   Note: Will fall back to local JSON if Fabric limitations are encountered"));

    // Register the service with automatic fallback for Fabric limitations
    builder.Services.AddScoped<ICustomerService>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();

        try
        {
            var cosmosService = ActivatorUtilities.CreateInstance<CustomerService>(sp, cosmosEndpoint!, databaseName, containerName);
            logger.LogInformation("✅ Successfully connected to Cosmos DB at {Endpoint}", cosmosEndpoint);
            return cosmosService;
        }
        catch (Exception ex) when (ex.Message.Contains("not supported for Azure Cosmos DB database in Microsoft Fabric"))
        {
            logger.LogWarning(ex, "Microsoft Fabric Cosmos DB limitation detected. Falling back to local JSON data.");
            return ActivatorUtilities.CreateInstance<LocalJsonCustomerService>(sp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to Cosmos DB. Falling back to local JSON data.");
            return ActivatorUtilities.CreateInstance<LocalJsonCustomerService>(sp);
        }
    });
}

var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
foreach (var (level, message) in startupLogs)
{
    startupLogger.Log(level, message);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

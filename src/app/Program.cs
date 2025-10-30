using CustomerDemoApp;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using CustomerDemoApp.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Configure Cosmos DB service
// Note: In a real app, these would come from configuration or environment variables
// For demo purposes, you'll need to update these values to match your Cosmos DB
var cosmosConnectionString = "YOUR_COSMOS_DB_CONNECTION_STRING_HERE";
var databaseName = "YOUR_DATABASE_NAME";
var containerName = "customers";

builder.Services.AddScoped(sp => new CosmosDbService(cosmosConnectionString, databaseName, containerName));

await builder.Build().RunAsync();
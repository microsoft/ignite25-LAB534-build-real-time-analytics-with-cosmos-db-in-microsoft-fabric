#:package Microsoft.Fabric.Api@1.0.0
#:package Azure.Identity@1.17.0
#:package Microsoft.Data.SqlClient@6.1.2

using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Fabric.Api;
using Microsoft.Fabric.Api.Core.Models;
using Microsoft.Fabric.Api.Warehouse.Models;


var defaultCapacityName = "fabricteamcapacity";
var workspaceName = "CommerceAnalytics";
var warehouseName = "CommerceWarehouse";

var credential = new AzureCliCredential();

var fabricClient = new FabricClient(credential);

var fabricCapacity = fabricClient.Core.Capacities.ListCapacities().FirstOrDefault(c => c.DisplayName == defaultCapacityName);

if (fabricCapacity == null)
{
    throw new Exception($"Capacity '{defaultCapacityName}' not found.");
}

var workspace = fabricClient.Core.Workspaces.ListWorkspaces().FirstOrDefault(w => w.DisplayName == workspaceName);
if (workspace == null)
{
    var createWorkspaceRequest = new CreateWorkspaceRequest(workspaceName)
    {
        CapacityId = fabricCapacity.Id,
    };
    var workspaceCreationResponse = await fabricClient.Core.Workspaces.CreateWorkspaceAsync(createWorkspaceRequest);
    workspace = workspaceCreationResponse.Value;
}

Warehouse? warehouse = null;

try
{
    var warehouseCreationResponse = await fabricClient.Warehouse.Items.CreateWarehouseAsync(workspace.Id, new CreateWarehouseRequest(warehouseName));
    warehouse = warehouseCreationResponse.Value;
}
catch (Exception ex) when (ex.Message.Contains("Failure getting LRO status"))
{
    // Ignore the  Azure.RequestFailedException: Failure getting LRO status
    // TODO: Investigate further why this exception occurs yet the warehouse is created successfully.
    // Try to retrieve the warehouse that was likely created despite the exception
    warehouse = fabricClient.Warehouse.Items.ListWarehouses(workspace.Id).FirstOrDefault(w => w.DisplayName == warehouseName);
}

if (warehouse == null)
{
    throw new Exception($"Failed to create or retrieve warehouse '{warehouseName}'.");
}


var warehouseConnectionString = $"Data Source={warehouse.Properties.ConnectionString},1433;Initial Catalog={warehouseName};Encrypt=True;TrustServerCertificate=False";

var tokenRequest = new TokenRequestContext(new[] { "https://database.windows.net/.default" });
var accessToken = await credential.GetTokenAsync(tokenRequest);

await using var sqlConnection = new SqlConnection(warehouseConnectionString)
{
    AccessToken = accessToken.Token
};

var createWarehouseTablesSqlFilePath = Path.Combine(Directory.GetCurrentDirectory(), "src", "create-data-warehouse.sql");
var createWarehouseTablesSqlCmdText = await File.ReadAllTextAsync(createWarehouseTablesSqlFilePath);

await sqlConnection.OpenAsync();
await using (var cmd = sqlConnection.CreateCommand())
{
    cmd.CommandText = createWarehouseTablesSqlCmdText;
    await cmd.ExecuteNonQueryAsync();
}

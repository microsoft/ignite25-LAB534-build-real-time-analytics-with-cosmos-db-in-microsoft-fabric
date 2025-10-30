//
// LoadData.cs — Provision a Fabric Warehouse and bulk-load CSV data.
//
// Pipeline
// 1) Authenticate via Azure CLI
// 2) Resolve Capacity and Workspace; create Workspace if missing
// 3) Create Warehouse (recover gracefully from transient LRO status issue)
// 4) Execute DDL from src/warehouse_setup/create-data-warehouse.sql
// 5) Load CSVs to tables (DimDate, DimTime, DimShop, DimMenuItem, FactSales) using a generic loader
//
// Usage
//   dotnet run .\src\warehouse_setup\LoadWarehouseData.cs
//
// Extensibility
// - Add a new TableSpec and call LoadTableFromCsvAsync to load additional tables.
// - Map differing CSV headers via ColumnSpec.CsvName.
// - Use ColumnSpec.SqlNameOverride for T-SQL keywords (e.g., [Year]).
//
// Important
// - The LRO comments below are intentionally preserved for ongoing investigation.
//
#:package Microsoft.Fabric.Api@1.*
#:package Azure.Identity@1.*
#:package Microsoft.Data.SqlClient@6.*
#:package CsvHelper@33.*
#:package Microsoft.Extensions.Logging@9.*
#:package Microsoft.Extensions.Logging.Console@9.*

using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Fabric.Api;
using Microsoft.Fabric.Api.Core.Models;
using Microsoft.Fabric.Api.Warehouse.Models;
using Microsoft.Fabric.Api.Lakehouse.Models;
using Microsoft.Extensions.Logging;

const string WarehouseName = "fc_commerce_wh";
const string LakehouseName = "fc_commerce_lh";

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
var logger = loggerFactory.CreateLogger("LoadWarehouseData");

var credential = new AzureCliCredential();
var fabricClient = new FabricClient(credential);

var WorkspaceName = Environment.GetEnvironmentVariable("FABRIC_WORKSPACE_NAME");
if (string.IsNullOrWhiteSpace(WorkspaceName))
{
    logger.LogError("Environment variable FABRIC_WORKSPACE_NAME is not set. Please set it to the target workspace name.");
    return;
}

var workspace = fabricClient.Core.Workspaces
    .ListWorkspaces()
    .FirstOrDefault(w => w.DisplayName == WorkspaceName);

if (workspace is null)
{
    logger.LogError("Workspace {WorkspaceName} not found. Follow the lab instructions to create it from the web", WorkspaceName);
    return;
}
else
{
    logger.LogInformation("Successfully retrieved workspace {WorkspaceName} with Id {WorkspaceId}", WorkspaceName, workspace.Id);
}


Lakehouse? lakehouse = null;
try
{
    logger.LogInformation("Attempting lakehouse creation for {LakehouseName} in workspace {WorkspaceId}", LakehouseName, workspace.Id);
    var lakehouseCreationResponse = await fabricClient.Lakehouse.Items.CreateLakehouseAsync(workspace.Id, new CreateLakehouseRequest(LakehouseName));
    lakehouse = lakehouseCreationResponse.Value;
    logger.LogInformation("Lakehouse created: {LakehouseName} Id {LakehouseId}", LakehouseName, lakehouse.Id);
}
catch (Exception ex) when (ex.Message.Contains("Failure getting LRO status", StringComparison.OrdinalIgnoreCase))
{
    // Ignore the  Azure.RequestFailedException: Failure getting LRO status
    // TODO: Investigate further why this exception occurs yet the warehouse is created successfully.
    // Try to retrieve the lakehouse that was likely created despite the exception
    logger.LogWarning(ex, "LRO status failure while creating lakehouse {LakehouseName}; attempting lookup", LakehouseName);
    lakehouse = await fabricClient.Lakehouse.Items.ListLakehousesAsync(workspace.Id)
        .FirstOrDefaultAsync(w => w.DisplayName == LakehouseName);
    if (lakehouse is not null)
        logger.LogInformation("Recovered lakehouse after LRO issue: {LakehouseName} Id {LakehouseId}", LakehouseName, lakehouse.Id);
}



Warehouse? warehouse = null;
try
{
    logger.LogInformation("Attempting warehouse creation for {WarehouseName} in workspace {WorkspaceId}", WarehouseName, workspace.Id);
    var warehouseCreationResponse = await fabricClient.Warehouse.Items.CreateWarehouseAsync(workspace.Id, new CreateWarehouseRequest(WarehouseName));
    warehouse = warehouseCreationResponse.Value;
    logger.LogInformation("Warehouse created: {WarehouseName} Id {WarehouseId}", WarehouseName, warehouse.Id);
}
catch (Exception ex) when (ex.Message.Contains("Failure getting LRO status", StringComparison.OrdinalIgnoreCase))
{
    // Ignore the  Azure.RequestFailedException: Failure getting LRO status
    // TODO: Investigate further why this exception occurs yet the warehouse is created successfully.
    // Try to retrieve the warehouse that was likely created despite the exception
    logger.LogWarning(ex, "LRO status failure while creating warehouse {WarehouseName}; attempting lookup", WarehouseName);
    warehouse = await fabricClient.Warehouse.Items.ListWarehousesAsync(workspace.Id)
        .FirstOrDefaultAsync(w => w.DisplayName == WarehouseName);
    if (warehouse is not null)
        logger.LogInformation("Recovered warehouse after LRO issue: {WarehouseName} Id {WarehouseId}", WarehouseName, warehouse.Id);
}

if (warehouse is null)
{
    logger.LogError("Failed to create or retrieve warehouse {WarehouseName}", WarehouseName);
    throw new InvalidOperationException($"Failed to create or retrieve warehouse '{WarehouseName}'. Potential silent failure after LRO status error; investigatory comments retained.");
}
logger.LogInformation("Using warehouse {WarehouseName} with Id {WarehouseId}", WarehouseName, warehouse.Id);

// Create a TXT file with all the important IDs
var idsFilePath = Path.Combine(Directory.GetCurrentDirectory(), "fabric-guids.txt");
var idsContent = new StringBuilder();
idsContent.AppendLine("Microsoft Fabric Resource IDs");
idsContent.AppendLine("Generated on: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
idsContent.AppendLine("=====================================");
idsContent.AppendLine();
idsContent.AppendLine($"Workspace Name: {WorkspaceName}");
idsContent.AppendLine($"Workspace ID: {workspace.Id}");
idsContent.AppendLine();
if (lakehouse?.Id is not null)
{
    idsContent.AppendLine($"Lakehouse Name: {LakehouseName}");
    idsContent.AppendLine($"Lakehouse ID: {lakehouse.Id}");
    idsContent.AppendLine();
}
idsContent.AppendLine($"Warehouse Name: {WarehouseName}");
idsContent.AppendLine($"Warehouse ID: {warehouse.Id}");
idsContent.AppendLine();
idsContent.AppendLine("=====================================");
idsContent.AppendLine("Use these IDs for connecting to your Fabric resources");

await File.WriteAllTextAsync(idsFilePath, idsContent.ToString());
logger.LogInformation("Created IDs file at: {IdsFilePath}", idsFilePath);


var warehouseConnectionString = $"Data Source={warehouse.Properties.ConnectionString},1433;Initial Catalog={WarehouseName};Encrypt=True;TrustServerCertificate=False";

var tokenRequest = new TokenRequestContext(new[] { "https://database.windows.net/.default" });
var accessToken = await credential.GetTokenAsync(tokenRequest);

await using var sqlConnection = new SqlConnection(warehouseConnectionString)
{
    AccessToken = accessToken.Token
};

var createWarehouseTablesSqlFilePath = Path.Combine(Directory.GetCurrentDirectory(), "src","warehouse_setup", "create-data-warehouse.sql");
if (!File.Exists(createWarehouseTablesSqlFilePath))
    throw new FileNotFoundException("Warehouse DDL script not found.", createWarehouseTablesSqlFilePath);

var createWarehouseTablesSqlCmdText = await File.ReadAllTextAsync(createWarehouseTablesSqlFilePath);

await sqlConnection.OpenAsync();
logger.LogInformation("Opened SQL connection to warehouse {WarehouseName}", WarehouseName);
await using (var cmd = sqlConnection.CreateCommand())
{
    cmd.CommandText = createWarehouseTablesSqlCmdText;
    logger.LogInformation("Executing schema creation script length {ScriptLength} characters", createWarehouseTablesSqlCmdText.Length);
    await cmd.ExecuteNonQueryAsync();
    logger.LogInformation("Schema creation script executed successfully");
}


var warehouseDataRootPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "relational");
var dimDateCsvPath  = Path.Combine(warehouseDataRootPath, "DimDate.csv");
var dimMenuCsvPath  = Path.Combine(warehouseDataRootPath, "DimMenu.csv");
var dimShopCsvPath  = Path.Combine(warehouseDataRootPath, "DimShop.csv");
var dimTimeCsvPath = Path.Combine(warehouseDataRootPath, "DimTime.csv");
var dimCustomerCsvPath  = Path.Combine(warehouseDataRootPath, "DimCustomer.csv");
var factSalesCsvPath = Path.Combine(warehouseDataRootPath, "FactSales.csv");
var factSalesLineItemsCsvPath = Path.Combine(warehouseDataRootPath, "FactSalesLineItem.csv");


// -----------------------
// Generic CSV -> Table loader
// -----------------------

/// <summary>
/// Stream a CSV into a destination table via OPENJSON, inserting in batches.
/// Validates required columns, supports optional dedupe on a key column, and logs progress.
/// </summary>
/// <param name="connection">Open SQL connection to the Fabric Warehouse.</param>
/// <param name="csvPath">Absolute path to the CSV file.</param>
/// <param name="table">Table specification: name and column specs.</param>
/// <param name="logger">ILogger for progress and diagnostics.</param>
/// <param name="batchSize">Number of rows to buffer per INSERT.</param>
/// <param name="dedupeKeyColumn">Optional CSV header used to skip duplicates (first occurrence wins).</param>
static async Task LoadTableFromCsvAsync(
    SqlConnection connection,
    string csvPath,
    TableSpec table,
    ILogger logger,
    int batchSize = 2_000,
    string? dedupeKeyColumn = null)
{
    if (!File.Exists(csvPath))
    {
        logger.LogError("Required data file {File} missing", csvPath);
        throw new FileNotFoundException($"CSV file not found for {table.TableName}.", csvPath);
    }

    logger.LogInformation("Beginning {Table} load from {FilePath}", table.TableName, csvPath);

    // Build the dynamic OPENJSON script for this table
    var columnList = string.Join(", ", table.Columns.Select(c => c.SqlName));
    var selectList = string.Join(", ", table.Columns.Select(c => c.SqlName));
    var withList = string.Join(",\n  ", table.Columns.Select(c => $"{c.SqlName} {c.SqlType} '$.{c.JsonName}'"));

    var cmdText = $@"
        DECLARE @j nvarchar(max) = @json;

        INSERT INTO dbo.{table.TableName}
        ({columnList})
        SELECT {selectList}
        FROM OPENJSON(@j)
        WITH (
        {withList}
        );";

    var insertedTotal = 0;
    var rows = new List<Dictionary<string, object?>>(batchSize);
    var seen = dedupeKeyColumn is null ? null : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    async Task FlushAsync()
    {
        if (rows.Count == 0) return;

        var json = SerializeJson(rows);
        using var cmd = new SqlCommand(cmdText, connection);
        cmd.Parameters.Add("@json", SqlDbType.NVarChar, -1).Value = json;
        var inserted = await cmd.ExecuteNonQueryAsync();
        insertedTotal += inserted;
        logger.LogDebug("{Table}: flushed batch of {Count} (cumulative {Total})", table.TableName, rows.Count, insertedTotal);
        rows.Clear();
    }

    using var sr = new StreamReader(csvPath);
    var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        PrepareHeaderForMatch = args => args.Header?.Trim().ToLowerInvariant(),
        DetectDelimiter = false,
        BadDataFound = null,
    };
    using var csv = new CsvReader(sr, csvConfig);

    await csv.ReadAsync();
    csv.ReadHeader();

    while (await csv.ReadAsync())
    {
        // Build row object for JSON
        var row = new Dictionary<string, object?>(table.Columns.Count, StringComparer.OrdinalIgnoreCase);

        // Optional dedupe on a specific column value (e.g., MenuItemKey in DimMenu.csv)
        if (dedupeKeyColumn is not null)
        {
            var k = GetRaw(csv, dedupeKeyColumn);
            if (k is null)
            {
                // If the key is missing but dedupe requested, skip or throw. Choose strict behavior.
                throw new InvalidOperationException($"Dedupe key '{dedupeKeyColumn}' missing in CSV for table {table.TableName}.");
            }
            if (!seen!.Add(k))
            {
                // Duplicate key -> skip subsequent rows
                continue;
            }
        }

        foreach (var col in table.Columns)
        {
            var raw = GetRaw(csv, col.CsvHeader);
            if (col.Required && string.IsNullOrWhiteSpace(raw))
            {
                throw new InvalidOperationException($"Required column '{col.CsvHeader}' is missing/empty for table {table.TableName}.");
            }

            object? value = col.Convert is null ? NormalizeDefault(raw) : col.Convert(raw);
            row[col.JsonName] = value;
        }

        rows.Add(row);
        if (rows.Count >= batchSize)
            await FlushAsync();
    }

    await FlushAsync();
    logger.LogInformation("Completed {Table} load from {FilePath}. Total rows inserted {Total}", table.TableName, csvPath, insertedTotal);

    // Local helpers
    static string? GetRaw(CsvReader csv, string header)
    {
        // headers normalized to lower-case in PrepareHeaderForMatch
        var target = header.Trim().ToLowerInvariant();
        return csv.TryGetField<string>(target, out var value) ? value : null;
    }

    static object? NormalizeDefault(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s; // let SQL cast via OPENJSON WITH types
}

// Serialize list of dictionaries into a JSON array with proper primitive typing
/// <summary>
/// Serialize buffered rows into a JSON array suitable for OPENJSON WITH projection.
/// Values are written with type-appropriate JSON tokens to avoid string casts.
/// </summary>
/// <summary>
/// Serialize buffered rows into a JSON array suitable for OPENJSON WITH projection.
/// Values are written with type-appropriate JSON tokens to avoid string casts.
/// </summary>
static string SerializeJson(IReadOnlyList<Dictionary<string, object?>> rows)
{
    using var ms = new MemoryStream();
    using (var writer = new Utf8JsonWriter(ms))
    {
        writer.WriteStartArray();
        foreach (var row in rows)
        {
            writer.WriteStartObject();
            foreach (var kvp in row)
            {
                WriteJsonValue(writer, kvp.Key, kvp.Value);
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }
    return Encoding.UTF8.GetString(ms.ToArray());
}

/// <summary>
/// Write a single JSON property using a suitable representation for primitives.
/// Strings are emitted using invariant culture; DateTime and TimeSpan normalized.
/// </summary>
/// <summary>
/// Write a single JSON property using a suitable representation for primitives.
/// Strings are emitted using invariant culture; DateTime and TimeSpan normalized.
/// </summary>
static void WriteJsonValue(Utf8JsonWriter writer, string name, object? value)
{
    if (value is null)
    {
        writer.WriteNull(name);
        return;
    }

    switch (value)
    {
        case int i:
            writer.WriteNumber(name, i);
            break;
        case long l:
            writer.WriteNumber(name, l);
            break;
        case decimal m:
            writer.WriteNumber(name, m);
            break;
        case double d:
            writer.WriteNumber(name, d);
            break;
        case bool b:
            writer.WriteBoolean(name, b);
            break;
        case DateTime dt:
            writer.WriteString(name, dt.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            break;
        case TimeSpan ts:
            // Ensure dot is escaped in custom TimeSpan format
            writer.WriteString(name, ts.ToString("hh\\:mm\\:ss\\.fff", CultureInfo.InvariantCulture));
            break;
        default:
            writer.WriteString(name, Convert.ToString(value, CultureInfo.InvariantCulture));
            break;
    }
}

// -----------------------
// Per-table specs and loads
// -----------------------

// Converters
/// <summary>Parse integer, returns null for empty.</summary>
static object? ToInt(string? s) => string.IsNullOrWhiteSpace(s) ? null : int.Parse(s, CultureInfo.InvariantCulture);
/// <summary>Parse long, returns null for empty.</summary>
static object? ToLong(string? s) => string.IsNullOrWhiteSpace(s) ? null : long.Parse(s, CultureInfo.InvariantCulture);
/// <summary>Parse decimal, returns null for empty.</summary>
static object? ToDecimal(string? s) => string.IsNullOrWhiteSpace(s) ? null : decimal.Parse(s, CultureInfo.InvariantCulture);
/// <summary>Parse boolean, accepts 0/1/true/false.</summary>
static object? ToBool(string? s)
{
    if (string.IsNullOrWhiteSpace(s)) return null;
    if (s == "0") return false;
    if (s == "1") return true;
    return bool.Parse(s);
}
/// <summary>Parse date (yyyy-MM-dd), returns null for empty.</summary>
static object? ToDate(string? s) => string.IsNullOrWhiteSpace(s) ? null : DateTime.Parse(s, CultureInfo.InvariantCulture);
/// <summary>Parse datetime with flexible input (UTC or local), normalized for datetime2(3).</summary>
static object? ToDateTime(string? s)
{
    if (string.IsNullOrWhiteSpace(s)) return null;
    // Handle inputs like "2025-10-15T10:00:00Z" and "2025-08-04 10:09:00.000"
    if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
        return dto.UtcDateTime; // normalized without trailing 'Z'
    return DateTime.Parse(s, CultureInfo.InvariantCulture);
}
/// <summary>Parse time (HH:mm:ss), normalized to HH:mm:ss.fff for time(3).</summary>
static object? ToTime(string? s)
{
    if (string.IsNullOrWhiteSpace(s)) return null;
    if (TimeSpan.TryParseExact(s, new[] { "hh\\:mm\\:ss", "h\\:mm\\:ss" }, CultureInfo.InvariantCulture, out var ts))
        return ts;
    return TimeSpan.Parse(s, CultureInfo.InvariantCulture);
}

// DimDate
var dimDateSpec = new TableSpec(
    "DimDate",
    new[]
    {
        new ColumnSpec("DateKey", "int", Required: true, Convert: ToInt),
        new ColumnSpec("FullDate", "date", Required: true, Convert: ToDate),
        new ColumnSpec("DayOfWeek", "int", Convert: ToInt),
        new ColumnSpec("DayName", "varchar(10)"),
        new ColumnSpec("MonthNumber", "int", Convert: ToInt),
        new ColumnSpec("MonthName", "varchar(10)"),
        new ColumnSpec("Quarter", "int", Convert: ToInt),
        new ColumnSpec("Year", "int", Convert: ToInt, SqlNameOverride: "[Year]"),
        new ColumnSpec("IsWeekend", "bit", Convert: ToBool),
        new ColumnSpec("IsHoliday", "bit", Convert: ToBool),
    }
);
await LoadTableFromCsvAsync(sqlConnection, dimDateCsvPath, dimDateSpec, logger);

// DimTime
var dimTimeSpec = new TableSpec(
    "DimTime",
    new[]
    {
        new ColumnSpec("TimeKey", "int", Required: true, Convert: ToInt),
        new ColumnSpec("FullTime", "time(3)", Required: true, Convert: ToTime),
        new ColumnSpec("Hour", "int", Convert: ToInt),
        new ColumnSpec("Hour12", "int", Convert: ToInt),
        new ColumnSpec("AMPM", "varchar(2)"),
        new ColumnSpec("TimeOfDay", "varchar(20)"),
        new ColumnSpec("BusinessPeriod", "varchar(20)"),
    }
);
await LoadTableFromCsvAsync(sqlConnection, dimTimeCsvPath, dimTimeSpec, logger);

// DimShop
var dimShopSpec = new TableSpec(
    "DimShop",
    new[]
    {
        new ColumnSpec("ShopKey", "int", Required: true, Convert: ToInt),
        new ColumnSpec("ShopId", "varchar(64)", Required: true),
        new ColumnSpec("ShopName", "varchar(128)"),
        new ColumnSpec("AirportId", "varchar(32)"),
        new ColumnSpec("AirportName", "varchar(128)"),
        new ColumnSpec("Terminal", "varchar(64)"),
        new ColumnSpec("Timezone", "varchar(64)"),
        new ColumnSpec("IsActive", "bit", Convert: ToBool),
        new ColumnSpec("CreatedAt", "datetime2(3)", Convert: ToDateTime),
        new ColumnSpec("UpdatedAt", "datetime2(3)", Convert: ToDateTime),
    }
);
await LoadTableFromCsvAsync(sqlConnection, dimShopCsvPath, dimShopSpec, logger);

// DimMenuItem (source CSV is DimMenu.csv with lowercase headers and extra columns; we map what we need and dedupe by MenuItemKey)
var dimMenuItemSpec = new TableSpec(
    "DimMenuItem",
    new[]
    {
        new ColumnSpec("MenuItemKey", "int", CsvName: "menuItemKey", Required: true, Convert: ToInt),
        new ColumnSpec("MenuItemId", "varchar(64)", CsvName: "menuItemId", Required: true),
        new ColumnSpec("MenuItemName", "varchar(128)", CsvName: "menuItemName"),
        new ColumnSpec("Category", "varchar(64)", CsvName: "category"),
        new ColumnSpec("Price", "decimal(10,2)", CsvName: "price", Convert: ToDecimal),
        new ColumnSpec("IsRecommended", "bit", Convert: _ => null), // not provided by CSV
        new ColumnSpec("Calories", "int", Convert: _ => null),       // not provided by CSV
        new ColumnSpec("IsActive", "bit", CsvName: "isActive", Convert: ToBool),
        new ColumnSpec("CreatedAt", "datetime2(3)", CsvName: "createdAt", Convert: ToDateTime),
        new ColumnSpec("UpdatedAt", "datetime2(3)", CsvName: "updatedAt", Convert: ToDateTime),
    }
);
// Deduplicate by MenuItemKey to satisfy PK; first occurrence wins
await LoadTableFromCsvAsync(sqlConnection, dimMenuCsvPath, dimMenuItemSpec, logger, dedupeKeyColumn: "menuItemKey");

//DimCustomer
var dimCustomerSpec = new TableSpec(
    "DimCustomer",
    new[]
    {
        new ColumnSpec("CustomerKey", "int", Required: true, Convert: ToInt),
        new ColumnSpec("CustomerId", "varchar(64)", Required: true),
        new ColumnSpec("CustomerName", "varchar(128)"),
        new ColumnSpec("Email", "varchar(128)"),
        new ColumnSpec("PreferredAirport", "varchar(32)"),
        new ColumnSpec("FavoriteDrink", "varchar(32)"),
        new ColumnSpec("IsActive", "bit", Convert: ToBool),
        new ColumnSpec("CreatedAt", "datetime2(3)", Convert: ToDateTime),
        new ColumnSpec("UpdatedAt", "datetime2(3)", Convert: ToDateTime),
    }
);

await LoadTableFromCsvAsync(sqlConnection, dimCustomerCsvPath, dimCustomerSpec, logger);

// FactSales
var factSalesSpec = new TableSpec(
    "FactSales",
    new[]
    {
        new ColumnSpec("SalesKey", "bigint", Required: true, Convert: ToLong),
        new ColumnSpec("TransactionId", "varchar(64)", Required: true),
        new ColumnSpec("DateKey", "int", Required: true, Convert: ToInt),
        new ColumnSpec("TimeKey", "int", Required: true, Convert: ToInt),
        new ColumnSpec("CustomerKey", "int", Required: true, Convert: ToInt),
        new ColumnSpec("ShopKey", "int", Convert: ToInt),
        new ColumnSpec("TotalQuantity", "int", Required: true, Convert: ToInt),
        new ColumnSpec("TotalAmount", "decimal(10,2)", Required: true, Convert: ToDecimal),
        new ColumnSpec("PaymentMethod", "varchar(32)"),
        new ColumnSpec("LoyaltyPointsEarned", "int", Convert: ToInt),
        new ColumnSpec("LoyaltyPointsRedeemed", "int", Convert: ToInt),
        new ColumnSpec("CreatedAt", "datetime2(3)", Convert: ToDateTime),
    }
);

await LoadTableFromCsvAsync(sqlConnection, factSalesCsvPath, factSalesSpec, logger);

var factSalesLineItemsSpec = new TableSpec(
    "FactSalesLineItems",
    new[]
    {
        new ColumnSpec("TransactionId", "varchar(64)", Required: true),
        new ColumnSpec("SalesKey", "bigint", Required: true, Convert: ToLong),
        new ColumnSpec("LineNumber", "int", Required: true, Convert: ToInt),
        new ColumnSpec("DateKey", "int", Required: true, Convert: ToInt),
        new ColumnSpec("TimeKey", "int", Required: true, Convert: ToInt),
        new ColumnSpec("MenuItemKey", "int", Required: true, Convert: ToInt),
        new ColumnSpec("Quantity", "int", Required: true, Convert: ToInt),
        new ColumnSpec("UnitPrice", "decimal(10,2)", Required: true, Convert: ToDecimal),
        new ColumnSpec("LineTotal", "decimal(10,2)", Required: true, Convert: ToDecimal),
        new ColumnSpec("PaymentMethod", "varchar(32)"),
        new ColumnSpec("Size", "varchar(32)"),
        new ColumnSpec("CreatedAt", "datetime2(3)", Convert: ToDateTime),
    }
);

await LoadTableFromCsvAsync(sqlConnection, factSalesLineItemsCsvPath, factSalesLineItemsSpec, logger);

/// <summary>
/// Column specification for a target table.
/// CsvName defaults to ColumnName but can be set to map differing headers.
/// SqlNameOverride can be used to escape keywords (e.g., [Year]).
/// </summary>
file readonly record struct ColumnSpec(
    string ColumnName,
    string SqlType,
    string? CsvName = null,
    bool Required = false,
    Func<string?, object?>? Convert = null,
    string? SqlNameOverride = null // e.g., "[Year]" for reserved words
)
{
    public string JsonName => ColumnName;
    public string CsvHeader => CsvName ?? ColumnName;
    public string SqlName => SqlNameOverride ?? ColumnName;
}

/// <summary>
/// Table specification bundles a name and its column definitions.
/// The order of Columns controls the OPENJSON projection and INSERT column order.
/// </summary>
file readonly record struct TableSpec(string TableName, IReadOnlyList<ColumnSpec> Columns);

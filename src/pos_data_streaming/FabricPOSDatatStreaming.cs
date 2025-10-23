//
// FabricPOSDatatStreaming.cs — Create a Fabric Eventstream and send POS events to its LocalStreamSource.
//
// Pipeline
// 1) Authenticate via Azure CLI
// 2) Resolve Workspace
// 3) Create Eventstream from src/pos_data_streaming/eventstream.json (base64 inline)
// 4) Resolve LocalStreamSource connection (Event Hubs compatible)
// 5) Produce JSON payloads at a fixed interval with graceful shutdown
//
// Usage
//   dotnet run .\src\pos_data_streaming\FabricPOSDatatStreaming.cs
//
// Notes
// - LRO handling mirrors LoadWarehouseData.cs to recover after transient status errors.
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
using Microsoft.Extensions.Logging;
using Microsoft.Fabric.Api;
using Microsoft.Fabric.Api.Core.Models;
using Microsoft.Fabric.Api.Eventstream.Models;

const string WorkspaceName = "Fourth Coffee Commerce - Lab 534";
const string EventstreamName = "sample-pos-event-stream";

// Structured logger, aligned with LoadWarehouseData.cs style
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

var credential = new AzureCliCredential();
var fabricClient = new FabricClient(credential);
logger.LogInformation("Starting POS event streaming setup in workspace {WorkspaceName}", WorkspaceName);

var workspace = fabricClient.Core.Workspaces
    .ListWorkspaces()
    .FirstOrDefault(w => w.DisplayName == WorkspaceName);

if (workspace is null)
{
    logger.LogError("Workspace {WorkspaceName} not found", WorkspaceName);
    throw new InvalidOperationException($"Workspace '{WorkspaceName}' not found.");
}
logger.LogInformation("Using workspace {WorkspaceName} with Id {WorkspaceId}", WorkspaceName, workspace.Id);

Eventstream? eventstream = null;

try
{
    var eventstreamJsonPayloadFilePath = Path.Combine(Directory.GetCurrentDirectory(), "src", "pos_data_streaming", "eventstream.json");
    if (!File.Exists(eventstreamJsonPayloadFilePath))
    {
        logger.LogError("Eventstream definition file missing: {Path}", eventstreamJsonPayloadFilePath);
        throw new FileNotFoundException("Eventstream definition (eventstream.json) not found.", eventstreamJsonPayloadFilePath);
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

    logger.LogInformation("Creating eventstream {EventstreamName} in workspace {WorkspaceId}", EventstreamName, workspace.Id);
    var eventstreamCreationResponse = await fabricClient.Eventstream.Items.CreateEventstreamAsync(workspace.Id, createEventstreamRequest);
    eventstream = eventstreamCreationResponse.Value;
    logger.LogInformation("Eventstream created: {EventstreamName} Id {EventstreamId}", EventstreamName, eventstream.Id);
}
catch (Exception ex) when (ex.Message.Contains("Failure getting LRO status", StringComparison.OrdinalIgnoreCase))
{
    // Ignore the  Azure.RequestFailedException: Failure getting LRO status
    // TODO: Investigate further why this exception occurs yet the eventstream is created successfully.
    // Try to retrieve the eventstream that was likely created despite the exception
    logger.LogWarning(ex, "LRO status failure while creating eventstream {EventstreamName}; attempting lookup", EventstreamName);
    eventstream = await fabricClient.Eventstream.Items.ListEventstreamsAsync(workspace.Id)
        .FirstOrDefaultAsync(es => es.DisplayName == EventstreamName);
    if (eventstream is not null)
        logger.LogInformation("Recovered eventstream after LRO issue: {EventstreamName} Id {EventstreamId}", EventstreamName, eventstream.Id);
}

if (eventstream is null || eventstream.Id is null)
{
    logger.LogError("Failed to create or retrieve eventstream {EventstreamName}", EventstreamName);
    throw new InvalidOperationException($"Failed to create or retrieve eventstream '{EventstreamName}'.");
}

// Resolve LocalStreamSource and obtain Event Hubs-compatible connection
var eventstreamTopologyResponse = await fabricClient.Eventstream.Topology.GetEventstreamTopologyAsync(workspace.Id, eventstream.Id.Value);
var eventstreamTopology = eventstreamTopologyResponse.Value;
var eventstreamLocalStreamSource = eventstreamTopology.Sources.FirstOrDefault(s => s.Name == "LocalStreamSource");

if (eventstreamLocalStreamSource?.Id is null)
{
    logger.LogError("LocalStreamSource not found for eventstream {EventstreamName}", EventstreamName);
    throw new InvalidOperationException("LocalStreamSource not found in eventstream topology.");
}

var eventstreamSourceConnection = await fabricClient.Eventstream.Topology.GetEventstreamSourceConnectionAsync(
    workspace.Id, eventstream.Id.Value, eventstreamLocalStreamSource.Id.Value);

var ehConnectionString = eventstreamSourceConnection.Value.AccessKeys.PrimaryConnectionString;
var ehName = eventstreamSourceConnection.Value.EventHubName;

await using var producer = new EventHubProducerClient(ehConnectionString, ehName);
logger.LogInformation("Initialized Event Hubs producer for {EventHub}", ehName);

// Graceful shutdown on Ctrl+C
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var random = new Random();
var deviceId = Environment.GetEnvironmentVariable("POS_DEVICE_ID") ?? "dev-001";
logger.LogInformation("Starting to send messages every 3 seconds. Press Ctrl+C to stop...");

try
{
    while (!cts.IsCancellationRequested)
    {
        var payload = new PayloadData(deviceId, DateTimeOffset.UtcNow, random.Next(1, 101));
        await using var batch = await producer.CreateBatchAsync(cts.Token);

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, PayloadJsonContext.Default.PayloadData));
        if (!batch.TryAdd(new EventData(bytes)))
        {
            // Very unlikely for this payload size; log and continue
            logger.LogWarning("Failed to add event to batch (size {Size} bytes)", bytes.Length);
        }
        else
        {
            await producer.SendAsync(batch, cts.Token);
            logger.LogInformation("Sent: {Payload}", Encoding.UTF8.GetString(bytes));
        }

        await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);
    }
}
catch (TaskCanceledException)
{
    // Expected on Ctrl+C
}
catch (Exception ex)
{
    logger.LogError(ex, "Unhandled error while producing events");
    throw;
}
finally
{
    logger.LogInformation("Shutting down event producer");
}

// Define a record for the payload
/// <summary>
/// Simple POS reading payload. "ts" is UTC and "value" is a demo metric.
/// </summary>
/// <param name="deviceId">Logical device identifier (can be overridden via POS_DEVICE_ID).</param>
/// <param name="ts">Timestamp in UTC.</param>
/// <param name="value">Reading value.</param>
file readonly record struct PayloadData(string deviceId, DateTimeOffset ts, int value);

// JSON source generator context
[JsonSerializable(typeof(PayloadData))]
file partial class PayloadJsonContext : JsonSerializerContext { }

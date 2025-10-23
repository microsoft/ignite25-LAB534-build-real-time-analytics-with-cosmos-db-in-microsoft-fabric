//
// FabricPOSDatatStreaming.cs — Create an Eventstream and send sample POS telemetry to its local source.
//
// Pipeline
// 1) Authenticate via Azure CLI
// 2) Resolve Workspace
// 3) Create Eventstream from JSON definition (recover from transient LRO status issue)
// 4) Resolve LocalStreamSource connection
// 5) Send JSON payloads to Event Hubs every 3 seconds until cancelled (Ctrl+C)
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
const string WorkspaceName = "Fourth Coffee Commerce - Lab 534";
const string EventstreamName = "sample-pos-stream";
const string DeviceId = "dev-001";
const int SendIntervalMs = 3000; // 3s cadence

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

// Azure auth and client
var credential = new AzureCliCredential();
var fabricClient = new FabricClient(credential);
logger.LogInformation("Starting POS data streaming. Workspace: {Workspace}", WorkspaceName);

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

var random = new Random();
logger.LogInformation("Sending messages every {Interval} ms. Press Ctrl+C to stop...", SendIntervalMs);

while (!cts.IsCancellationRequested)
{
    try
    {
        var payload = new PayloadData(DeviceId, DateTimeOffset.UtcNow, random.Next(1, 101));
        using var batch = await producer.CreateBatchAsync(cts.Token);
        var payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, PayloadJsonContext.Default.PayloadData));
        if (!batch.TryAdd(new EventData(payloadBytes)))
        {
            logger.LogWarning("Payload too large for batch; skipping message");
        }
        else
        {
            await producer.SendAsync(batch, cts.Token);
            logger.LogInformation("Sent: {Payload}", JsonSerializer.Serialize(payload, PayloadJsonContext.Default.PayloadData));
        }
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    {
        // Graceful shutdown
        break;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error while sending event; continuing");
    }

    await Task.Delay(SendIntervalMs, cts.Token);
}

logger.LogInformation("Stopped POS data streaming");

// Define a record for the payload
internal readonly record struct PayloadData(string deviceId, DateTimeOffset ts, int value);

// JSON source generator context
[JsonSerializable(typeof(PayloadData))]
internal partial class PayloadJsonContext : JsonSerializerContext { }

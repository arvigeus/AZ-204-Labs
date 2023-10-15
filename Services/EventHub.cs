using System.Text;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Producer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

// EventHubProducerClient: Source of various types of data such as telemetry, diagnostics, logs, etc.
// EventProcessorClient: 
// - Handle data from several partitions (distributed ownership).
// - Multiple instances can be used to scale/balance load.
// - Marks the last processed event within a partition (checkpointing) - resume after failure. Requires storage account.
// EventHubConsumerClient
// - Read EventData from a specific consumer group
// - Exlusive (Epoch) - only one reader; Non-inclusive (Non-Epoch) - allow multiple consumers from the same consumer group.

// Message ordering: FIFO per partition

// Partitioning: Divides the message stream into smaller, ordered sequences for parallel data processing and increased throughput. Increases processing time
// - New events added in the order they were received

// Namespace: <namespace>.servicebus.windows.net. Throughput unit are specified here.

// Consumer Groups: Allows multiple applications to read the event stream independently.

// Batch Processing: Uses partitioned consumer model to process streams concurrently and control processing speed.

// Event Receivers: Entities that read event data through:
// - HTTPS: faster initialization, slower (use for less frequent publisher)
// - AMQP: higher throughput and lower latency for frequent publishers

// Max event retention: 7 days (standard), 90 days (premium and dedicated)
// - Use Event Hubs Capture to store for longer

// Event Hubs Capture
// - Streaming data into Azure Blob storage or Azure Data Lake Storage (in any region)
// - Format (avro): https://{storageAccount}.blob.core.windows.net/{containerName}/{eventHubNamespace}/{eventHubName}/{partitionId}/{year}/{month}/{day}/{hour}/{minute}/{second}.avro
//   - Log Compaction: use key-based retention, instead of time
// - Capture windowing: minimum size and time window for capturing data
// - First wins policy: initiated by the first trigger encountered
// - Each partition independently captures data and names a block blob after the capture interval is reached.

// Roles:
// - Azure Event Hubs Data Owner: complete access
// - Azure Event Hubs Data Sender: send access
// - Azure Event Hubs Data Receiver: receiving access

class EventHubService
{
    static string serviceBusEndpoint = "example-namespace.servicebus.windows.net/";
    string connectionString = $"Endpoint=sb://{serviceBusEndpoint};SharedAccessKeyName=KeyName;SharedAccessKey=AccessKey";
    string eventHubName = "example-event-hub";
    string consumerGroup = EventHubConsumerClient.DefaultConsumerGroupName;

    string queueName = "az204-queue";

    // EventHubProducerClient ManagedIdentityEventHubProducerClient() => new(serviceBusEndpoint, eventHubName, new DefaultAzureCredential());
    // EventHubProducerClient ConnectionStringEventHubProducerClient() => new(connectionString: $"Endpoint=sb://{serviceBusEndpoint};SharedAccessKeyName=KeyName;SharedAccessKey=AccessKey", eventHubName);
    // EventHubProducerClient NamedKeyCredentialEventHubProducerClient() => new(serviceBusEndpoint, eventHubName, new AzureNamedKeyCredential("sharedAccessKeyName", "sharedAccessKey"));

    async Task SendBatch()
    {
        await using (var producerClient = new EventHubProducerClient(connectionString, eventHubName))
        {
            using EventDataBatch eventBatch = await producerClient.CreateBatchAsync();
            eventBatch.TryAdd(new EventData(Encoding.UTF8.GetBytes("First event")));
            eventBatch.TryAdd(new EventData("Second event"));
            await producerClient.SendAsync(eventBatch);
        }
    }

    async Task Partitioning()
    {
        await using (var producerClient = new EventHubProducerClient(connectionString, eventHubName))
        {
            string[] partitionIds = await producerClient.GetPartitionIdsAsync(); // Query partition IDs

            using EventDataBatch eventBatch = await producerClient.CreateBatchAsync(new CreateBatchOptions { PartitionKey = partitionIds[0] });
            eventBatch.TryAdd(new EventData(Encoding.UTF8.GetBytes("First event")));
            eventBatch.TryAdd(new EventData("Second event"));
            await producerClient.SendAsync(eventBatch);
        }
    }

    async Task UsingBuffer()
    {
        await using (var bufferedProducerClient = new EventHubBufferedProducerClient(connectionString, eventHubName))
        {
            await bufferedProducerClient.EnqueueEventAsync(new EventData(Encoding.UTF8.GetBytes("First event")));
            await bufferedProducerClient.EnqueueEventAsync(new EventData(Encoding.UTF8.GetBytes("Second event")));
            await bufferedProducerClient.EnqueueEventsAsync(new[] { new EventData("Third Event") });
        }
    }

    async Task ConsumeEvents()
    {
        await using (var consumer = new EventHubConsumerClient(consumerGroup, connectionString, eventHubName))
        {
            // All events
            await foreach (PartitionEvent receivedEvent in consumer.ReadEventsAsync()) { } // Wait for events

            // Events from partition - needs partition and starting position
            EventPosition startingPosition = EventPosition.Earliest;
            string partitionId = (await consumer.GetPartitionIdsAsync()).First();
            await foreach (PartitionEvent receivedEvent in consumer.ReadEventsFromPartitionAsync(partitionId, startingPosition)) // Wait for events in partition
            {
                string readFromPartition = receivedEvent.Partition.PartitionId;
                byte[] eventBody = receivedEvent.Data.EventBody.ToArray();
            }
        }
    }

    async Task UseEventProcessor()
    {
        // You need Blob Storage for checkpointing
        string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=exampleaccount;AccountKey=examplekey;EndpointSuffix=core.windows.net";
        string blobContainerName = "example-container";
        var storageClient = new BlobContainerClient(storageConnectionString, blobContainerName);

        var processor = new EventProcessorClient(storageClient, consumerGroup, connectionString, eventHubName);

        processor.ProcessEventAsync += async (ProcessEventArgs eventArgs) =>
        {
            // Checkpointing: Update checkpoint in the blob storage so that you can resume from this point if the processor restarts
            await eventArgs.UpdateCheckpointAsync();
        };

        processor.ProcessErrorAsync += (ProcessErrorEventArgs eventArgs) => Task.CompletedTask;

        await processor.StartProcessingAsync();
        try { await Task.Delay(Timeout.Infinite, new CancellationTokenSource(TimeSpan.FromSeconds(45)).Token); } catch (TaskCanceledException) { }
        await processor.StopProcessingAsync();
    }
}

class EventHubFunctions
{
    [FunctionName(nameof(ReceiveMessagesAsBatch))]
    public static void ReceiveMessagesAsBatch([EventHubTrigger(eventHubName: "hub", Connection = "EventHubConnectionAppSetting")] EventData[] events, ILogger log)
    {
        foreach (EventData message in events)
        {
            log.LogInformation($"Message: {Encoding.UTF8.GetString(message.EventBody)}");
            log.LogInformation($"System Properties: {JsonConvert.SerializeObject(message.SystemProperties)}");
        }
    }

    [FunctionName(nameof(OutputEventHubMessage))]
    [return: EventHub(eventHubName: "hub", Connection = "EventHubConnectionAppSetting")]
    public static string OutputEventHubMessage(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "event/{message}")] HttpRequest req, string message,
        ILogger log)
    {
        log.LogInformation($"Event sent: {message}");
        return message;
    }

    [FunctionName(nameof(RedirectMessagesToAnotherHubWithPartitioning))]
    public static async Task RedirectMessagesToAnotherHubWithPartitioning(
    [EventHubTrigger("source", Connection = "EventHubConnectionAppSetting")] EventData[] events,
    [EventHub("dest", Connection = "EventHubConnectionAppSetting")] IAsyncCollector<EventData> outputEvents,
    ILogger log)
    {
        foreach (EventData message in events)
        {
            string newMessage = Encoding.UTF8.GetString(message.EventBody);
            await outputEvents.AddAsync(new EventData(newMessage));

            // Group events together by partition key    
            await outputEvents.AddAsync(new EventData(newMessage), partitionKey: "sample-key");
        }
    }
}
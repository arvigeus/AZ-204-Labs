using System.Reflection.Metadata;
using Azure.Messaging.EventGrid;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Services;

// Database: consistency
// Container: partition key

// Throughput:
// - container - dedicated,
// - database - shared
// - serverless - pay as you go
// - Autoscale - scales to meet a target

// Stored procedures: get context; getResponse() or getCollection(); response.getBody() or container.createDocument();

// Composite index for 2+ order by

// Conflict resolution: auto(last wins), custom

// Change feed: order per partition key
// - Monitored container: Holds data for change feed. Reflects inserts/updates.
// - Lease container: State storage, coordinates feed processing across workers. Can be in same/different account as monitored container.
// - Delegate component: Custom logic for processing changes.
// - Compute Instance: Hosts processor; can be VM, Kubernetes pod, Azure App Service, or physical machine.

// Best practices:
// - Latest SDK
// - Use single instance of `CosmosClient`
// - `Direct` mode for ⚡
// - Retry logic for handling transient errors
// - Read 🏋🏿: `Stream API` and `FeedIterator`
// - Write 🏋🏿: Enable bulk support, set `EnableContentResponseOnWrite` to false, exclude unused paths from indexing and keep the size of your documents minimal

class CosmosDBService
{
    async Task Query()
    {
        var client = new CosmosClient("AccountEndpoint=https://<your-account-name>.documents.azure.com:443/;AccountKey=<your-account-key>;Database=<your-database-name>;");

        // var database = await client.CreateDatabaseAsync("db", ThroughputProperties.CreateAutoscaleThroughput(autoscaleMaxThroughput: 700));
        var database = client.GetDatabase("db");

        Container container = await database.CreateContainerIfNotExistsAsync(id: "<container>", partitionKeyPath: "/Group", throughput: 700); // Note: This returns ContainerResponse object, but we go around it
        // var container = await database.CreateContainerAsync(new ContainerProperties(id: "container", partitionKeyPath: "/name"));
        // var container = database.GetContainer("<container>");

        var created = await container.CreateItemAsync(new Item { Name = "1", Group = "MyPartitionValue" }, new PartitionKey("Group")); // No slash

        string queryText = "select * from items s where s.Name = @NameInput ";
        QueryDefinition query = new QueryDefinition(queryText)
            .WithParameter("@NameInput", "Account1");
        FeedIterator<Item> feedIterator = container.GetItemQueryIterator<Item>(
        query // Note: you can pass queryText directly here
        // Optional:
        // requestOptions: new QueryRequestOptions()
        // {
        //     PartitionKey = new PartitionKey("Account1"),
        //     MaxItemCount = 1
        // }
        );
        while (feedIterator.HasMoreResults)
        {
            FeedResponse<Item> response = await feedIterator.ReadNextAsync();
            foreach (var item in response) Console.WriteLine(item.Name);
        }

        var queryable = container
            .GetItemLinqQueryable<Item>()
            .Where(item => item.Age > 25 && item.Group == "MyPartitionValue")
            .ToFeedIterator();
        while (queryable.HasMoreResults) { }
    }

    class Item
    {
        public string Name { get; set; } = "";
        public int Age { get; set; } = 0;
        public string Group { get; set; } = ""; // Partition key
    }

    async Task<ChangeFeedProcessor> StartChangeFeedProcessorAsync(CosmosClient cosmosClient)
    {
        Container monitoredContainer = cosmosClient.GetContainer("databaseName", "monitoredContainerName");
        Container leaseContainer = cosmosClient.GetContainer("databaseName", "leaseContainerName");

        ChangeFeedProcessor changeFeedProcessor = monitoredContainer
            .GetChangeFeedProcessorBuilder<ToDoItem>(processorName: "changeFeedSample", onChangesDelegate: DelagateHandleChangesAsync)
                .WithInstanceName("consoleHost") // Compute Instance
                .WithLeaseContainer(leaseContainer)
                .Build();

        Console.WriteLine("Starting Change Feed Processor...");
        await changeFeedProcessor.StartAsync();
        Console.WriteLine("Change Feed Processor started.");
        return changeFeedProcessor;
    }

    static async Task DelagateHandleChangesAsync(
        ChangeFeedProcessorContext context,
        IReadOnlyCollection<ToDoItem> changes,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Started handling changes for lease {context.LeaseToken}...");
        Console.WriteLine($"Change Feed request consumed {context.Headers.RequestCharge} RU.");
        // SessionToken if needed to enforce Session consistency on another client instance
        Console.WriteLine($"SessionToken ${context.Headers.Session}");

        foreach (ToDoItem item in changes)
        {
            Console.WriteLine($"Detected operation for item with id {item.id}.");
            await Task.Delay(10);
        }
    }
}

class CosmoDBFunctions
{
    [FunctionName("LogItems")]
    public static void LogItems(
    [CosmosDBTrigger(
        databaseName: "ecommerceDB",
        containerName: "orders",
        Connection = "CosmosDBConnection",
        LeaseContainerName = "leases",
        CreateLeaseContainerIfNotExists = true)]
    IReadOnlyList<ToDoItem> input, // Can be 
    ILogger log)
    {
        if (input == null) return;

        log.LogInformation("Documents modified " + input.Count);
        foreach (var todo in input)
        {
            log.LogInformation("First document Id " + todo.id);
        }
    }

    [FunctionName("SyncToAnotherContainer")]
    public static async Task SyncToAnotherContainer(
    [CosmosDBTrigger(
        databaseName: "sourceDB",
        containerName: "sourceContainer",
        Connection = "CosmosDBConnection",
        LeaseContainerName = "leases")]
    IReadOnlyList<ToDoItem> input,
    [CosmosDB(
        databaseName: "destinationDB",
        containerName: "destinationContainer",
        Connection = "CosmosDBConnection")]
    IAsyncCollector<object> output,
    ILogger log)
    {
        if (input == null || input.Count == 0) return;

        foreach (var document in input)
        {
            await output.AddAsync(document);
        }
    }

    [FunctionName("SendNotificationOnUpdate")]
    public static void Run(
    [CosmosDBTrigger(
        databaseName: "notificationDB",
        containerName: "events",
        Connection = "CosmosDBConnection",
        LeaseContainerName = "leases")]
    IReadOnlyList<Document> input,
    [EventGrid(TopicEndpointUri = "EventGridTopicUri", TopicKeySetting = "EventGridTopicKey")]
    ICollector<EventGridEvent> outputEvents,
    ILogger log)
    {
        if (input != null && input.Count > 0)
        {
            foreach (var document in input)
            {
                var eventGridEvent = new EventGridEvent(subject: "New Event", eventType: "CosmosDB.ItemUpdated", dataVersion: "1.0", data: document);
                outputEvents.Add(eventGridEvent);
            }
        }
    }

    [FunctionName("EventGridOutputBinding")]
    [return: EventGrid(TopicEndpointUri = "EventGridTopicUriAppSetting", TopicKeySetting = "EventGridTopicKeyAppSetting")]
    public static async Task<EventGridEvent> RunEventGridOutputBinding(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "event/{subject}")] HttpRequest req, string subject,
    ILogger log)
    {
        // Sends an event to Event Grid Topic
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var eventGridEvent = new EventGridEvent(subject, "MyEventType", requestBody, "1.0");
        log.LogInformation($"Event sent: {subject}");
        return eventGridEvent;
    }
}

public class ToDoItem
{
    public string id { get; set; } = "";
    public string Description { get; set; } = "";
}
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Services;

// ServiceBusClient
// ServiceBusSender - (publishers)
// ServiceBusProcessor - (subscribers) for consuming messages (via handlers)

// Publishers send messages to a topic (1:n), and each message is distributed to all subscriptions registered with the topic.
// - Broadcast Pattern: Every subscription gets a copy of each message.
// - Partitioning Pattern: Distributes messages across subscriptions in a mutually exclusive manner.
// - Routing Pattern: When you need to route messages based on their content or some attributes.

// Message Routing and Correlation
// - Simple request/reply: Queue-based, uses ReplyTo and MessageId for replies.
// - Multicast request/reply: Topic-based, multiple subscribers, optional topic in ReplyTo.
// - Multiplexing: Single queue or subscription, groups messages via SessionId.
// - Multiplexed request/reply: Shared reply queue, guided by ReplyToSessionId and SessionId.
// Applications can also use user properties for routing, as long as they don't use the reserved `To` property.

// Subscriptions act like virtual queues and can apply filters to receive specific messages.
// Subscribers receive messages from the topic. Only one can receives and processes each message at a time from queues (Point-to-Point connection)
// Receive modes:
// - Receive and delete: message immediately removed from queue
// - Peek lock: message will be removed after being marked as complete (ProcessMessageEventArgs.CompleteMessageAsync()), or after timeout

// Message ordering: FIFO per session (sessionId)
// - sessionId also allows processing messages as parallel, long-running streams

// Namespace: <namespace>.servicebus.windows.net.

// Protocols: HTTPS and HTTPS (see Event Hub)

// Message factories: For high throughput with many senders/receivers, use multiple factories. For imbalance, use one factory per process.

// ## TTL
// - Message-level TTL cannot be higher than topic's (queue) TTL. If not set, queue's TTL is used.
// - When a message is locked, its expiration is halted until the lock expires or the message is abandoned.

// Premium plan: Up to 100MB messages (compared to 256KB), fixed pricing (compared to "pay as you go"), high throughput.

// Filters
// - SQL:  SQL-like, complex conditions (not, comparison, etc). Lower throughput. System properties must be prefixed with `sys.`
// - Boolean: All or none messages.
// - Correlation: Match on specific properties, like Subject & CorrelationId. Higher efficiency.
// Actions: Modify properties post-match.

// Load-leveling: System is optimized to manage the average load, instead of peaks.
// Autoforwarding: Transfers messages within namespace
// Dead-letter queue: Stores undeliverable messages (automatic). Can store expired messages as well
// Scheduled delivery: Delays messages until set time using ScheduledEnqueueTimeUtc property
// Message deferral: Sets aside messages for later retrieval
// Batching: Better throughput, worse latency
// Transactions: Groups operations for single entity
// Autodelete on idle
// Duplicate detection
// Geo-disaster recovery: Switches to alternate region during downtime

class ServiceBusService
{
    static string serviceBusEndpoint = "example-namespace.servicebus.windows.net/";
    string connectionString = $"Endpoint=sb://{serviceBusEndpoint};SharedAccessKeyName=KeyName;SharedAccessKey=AccessKey";
    string queueName = "az204-queue";

    // ServiceBusClient ManagedIdentityServiceBus() => new(serviceBusEndpoint, new DefaultAzureCredential());
    // ServiceBusClient ConnectionStringServiceBus() => new(connectionString);
    // ServiceBusClient NamedKeyCredentialServiceBus() => new(serviceBusEndpoint, new AzureNamedKeyCredential("sharedAccessKeyName", "sharedAccessKey"));

    async Task SendBatch()
    {
        await using ServiceBusClient client = new ServiceBusClient(connectionString);
        await using ServiceBusSender sender = client.CreateSender(queueName);
        using ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync();
        for (int i = 1; i <= 3; i++)
            if (!messageBatch.TryAddMessage(new ServiceBusMessage($"Message {i}")))
                throw new Exception($"Exception {i} has occurred.");
        await sender.SendMessagesAsync(messageBatch);
    }

    async Task SendMessage()
    {
        await using ServiceBusClient client = new ServiceBusClient(connectionString);
        await using ServiceBusSender sender = client.CreateSender(queueName);
        await sender.SendMessageAsync(new ServiceBusMessage("Message"));
        await sender.SendMessageAsync(new ServiceBusMessage(new BinaryData(new Person { Name = "John", Age = 30 })));
    }

    async Task ReceiveMessage()
    {
        await using ServiceBusClient client = new ServiceBusClient(connectionString);
        await using ServiceBusReceiver receiver = client.CreateReceiver(queueName);
        var receivedMessage = await receiver.ReceiveMessageAsync();
        var receivedPerson = receivedMessage.Body.ToObjectFromJson<Person>();
    }

    async Task UseProcessor() // Note: No checkpointing here, only completion
    {
        await using ServiceBusClient client = new ServiceBusClient(connectionString);
        await using ServiceBusSender sender = client.CreateSender(queueName);
        await using ServiceBusProcessor processor = client.CreateProcessor(queueName, new ServiceBusProcessorOptions());

        processor.ProcessMessageAsync += async (ProcessMessageEventArgs args) =>
        {
            string body = args.Message.Body.ToString();// payload is an opaque binary block, format described in `ContentType` property
            Console.WriteLine($"Received: {body}");
            await args.CompleteMessageAsync(args.Message);
        };

        processor.ProcessErrorAsync += (ProcessErrorEventArgs args) =>
        {
            Console.WriteLine(args.Exception.ToString());
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync();
        try { await Task.Delay(Timeout.Infinite, new CancellationTokenSource(TimeSpan.FromSeconds(45)).Token); } catch (TaskCanceledException) { }
        await processor.StopProcessingAsync();
    }

    async Task FiltersAndActions()
    {
        var adminClient = new ServiceBusAdministrationClient(connectionString);

        await adminClient.CreateSubscriptionAsync(
            new CreateSubscriptionOptions("topicName", "subscriptionName"),
            new CreateRuleOptions("BlueSize10Orders", new SqlRuleFilter("color='blue' AND quantity=10"))
        );

        await adminClient.CreateRuleAsync("topicName", "subscriptionName", new CreateRuleOptions
        {
            Name = "RedOrdersWithAction",
            Filter = new SqlRuleFilter("user.color='red'"),
            Action = new SqlRuleAction("SET quantity = quantity / 2;")
        });

        await adminClient.CreateSubscriptionAsync(
            new CreateSubscriptionOptions("topicName", "subscriptionName"),
            new CreateRuleOptions("AllOrders", new TrueRuleFilter())
        );

        await adminClient.CreateSubscriptionAsync(
            new CreateSubscriptionOptions("topicName", "subscriptionName"),
            new CreateRuleOptions("HighPriorityRedOrdersRule", new CorrelationRuleFilter()
            {
                Subject = "red",
                CorrelationId = "high"
            })
        );
    }

    class Person
    {
        public string? Name { get; set; }
        public int? Age { get; set; }
    }
}

class ServiceBusFunctions
{
    [FunctionName("ServiceBusOutputBinding")]
    [return: ServiceBus("queue", Connection = "ServiceBusConnectionAppSetting")]
    public static string RunServiceBusOutputBinding(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "servicebus/{message}")] HttpRequest req, string message,
    ILogger log)
    {
        // Sends a message to Service Bus Queue
        log.LogInformation($"Message sent: {message}");
        return message;
    }
}


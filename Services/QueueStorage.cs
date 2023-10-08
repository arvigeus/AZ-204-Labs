using System.Configuration;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

// Endpoint: `https://queue.core.windows.net`

// - May contain millions of messages, up to the total capacity limit of a storage account.
// - Commonly used to create a backlog of work to process asynchronously.
// - Max size: 64KB
// - TTL: 7 days(⏺️), -1 to never expire.
// - Applications can scale indefinitely to meet demand.
// - Receive mode: Peek & Lease  

class StorageQueueService
{
    string connectionString = ConfigurationManager.AppSettings["StorageConnectionString"] ?? throw new Exception();
    string queueName = "storagequeue";

    async Task Handle()
    {
        // Instantiate a QueueClient which will be used to create and manipulate the queue
        QueueClient queueClient = new QueueClient(connectionString, queueName);

        // Create the queue if it doesn't already exist
        await queueClient.CreateIfNotExistsAsync();

        if (await queueClient.ExistsAsync())
        {
            await queueClient.SendMessageAsync("message");

            // Peek at the next message
            // If you don't pass a value for the `maxMessages` parameter, the default is to peek at one message.
            PeekedMessage[] peekedMessages = await queueClient.PeekMessagesAsync();

            // Change the contents of a message in-place
            // This code saves the work state and grants the client an extra minute to continue their message (default is 30 sec).
            QueueMessage[] message = await queueClient.ReceiveMessagesAsync();
            // PopReceipt must be provided when performing operations to the message
            // in order to prove that the client has the right to do so when locked
            queueClient.UpdateMessage(message[0].MessageId,
                    message[0].PopReceipt,
                    "Updated contents",
                    TimeSpan.FromSeconds(60.0)  // Make it invisible for another 60 seconds
                );

            // Dequeue the next message
            QueueMessage[] retrievedMessage = await queueClient.ReceiveMessagesAsync();
            Console.WriteLine($"Dequeued message: '{retrievedMessage[0].Body}'");
            await queueClient.DeleteMessageAsync(retrievedMessage[0].MessageId, retrievedMessage[0].PopReceipt);

            // Get the queue length
            QueueProperties properties = await queueClient.GetPropertiesAsync();
            int cachedMessagesCount = properties.ApproximateMessagesCount; // >= of actual messages count
            Console.WriteLine($"Number of messages in queue: {cachedMessagesCount}");

            // Delete the queue
            await queueClient.DeleteAsync();
        }
    }
}

class QueueStorageFunctions
{
    [FunctionName(nameof(RunQueue))]
    public static void RunQueue([QueueTrigger("queue", Connection = "StorageConnectionAppSetting")] string myQueueItem, ILogger log)
    {
        log.LogInformation($"Queue trigger function processed: {myQueueItem}");
    }

    // No Input binding

    [FunctionName(nameof(QueueStorageOutputBinding))]
    [return: Queue("queue")]
    public static string QueueStorageOutputBinding(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "queue/{message}")] HttpRequest req, string message,
        ILogger log)
    {
        // Sends a message to Azure Queue Storage
        log.LogInformation($"Message sent: {message}");
        return message;
    }

    [FunctionName(nameof(AddMessages))]
    public static void AddMessages(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
    [Queue("outqueue"), StorageAccount("AzureWebJobsStorage")] ICollector<string> msg,
    ILogger log)
    {
        msg.Add("First");
        msg.Add("Second");
    }
}
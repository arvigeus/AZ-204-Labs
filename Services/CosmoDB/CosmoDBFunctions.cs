using System.Reflection.Metadata;
using System.Text;
using Azure.Core;
using Azure.Identity;
using Azure.Messaging.EventGrid;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Azure.WebJobs.Extensions.EventHubs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.ServiceBus;
using Microsoft.Extensions.Logging;

namespace Services;

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
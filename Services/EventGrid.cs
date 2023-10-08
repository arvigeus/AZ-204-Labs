using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Services;

// Focuses on event, not payload
// No event order guarantee
// Messages as array, 1MB max total, charged per 64KB
// Webhooks: New event triggers HTTP POST to endpoint.

// Schemas:
// - EventGridEvent: specific to Azure only; `"content-type":"application/json;`
// - CloudSchema (recommended): can be used across different cloud providers and platforms; supports bidirectional event transformation; `"content-type":"application/cloudevents+json;`

// Targets:
// - Webhooks
// - Azure Service Bus topics and queues (up to 80GB in total)
// - Azure Storage Queue (up to 64KB per message)
// - Azure Event Hubs
// - Azure Functions

// Retry policies: Dead-lettering on 4XX response (set/unset with --deadletter-endpoint).
// - Webhooks are retried until 200 - OK
// - Azure Storage Queue retries until successful processing
// Exponentially delaying delivery attempts for unhealty endpoints

// Batching: All or None only.
// Optimistic Batching: Batching is at best effort, not strictly

// Subscriptions roles don't grant access for actions such as creating topics.
// Permissions needed to subscribe to event handlers (except WebHooks): Microsoft.EventGrid/EventSubscriptions/Write

// Validation: endpoint must return 200 and:
// - validationCode (sync)
// - validationUrl (async)
// Automatically handled for:
// - Azure Functions with Event Grid Trigger
// - Azure Automation via webhook
// - Azure Logic Apps with Event Grid Connector

// No self-signed certificates, only commercial

class EventGridService
{
    async Task SendMessage()
    {
        Uri endpoint = new Uri("https://<topic-name>.<location>.eventgrid.azure.net/api/events");

        var credential = new AzureKeyCredential("<EventGrid-Topic-Key>"); // key for Event Grid topic, which you can find in the Azure Portal
        // var credential = new DefaultAzureCredential();

        var data = new object();

        var client = new EventGridPublisherClient(endpoint, credential);

        await client.SendEventAsync(new EventGridEvent(
            subject: "Object.New", // mandatory in schema
            eventType: "EventGridEvent.New",
            dataVersion: "1.0",
            data: data
        ));

        await client.SendEventAsync(new CloudEvent(
            source: "Object.New", // mandatory in schema
            type: "CloudEvent.New", // mandatory in schema
            jsonSerializableData: JsonConvert.SerializeObject(data)
        ));
    }
}

class EventGridFunctions
{
    // Instead of TopicEndpointUri and TopicKeySetting, simply pass "<TopicKeySetting>__topicEndpointUri" to TopicEndpointUri
    [FunctionName("WithTopicEndpointUriOnly")]
    [return: EventGrid(TopicEndpointUri = "EventGridTopicKeyAppSetting__topicEndpointUri")]
    public static EventGridEvent WithTopicEndpointUriOnly(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, string subject,
    ILogger log) => new EventGridEvent(subject: subject, eventType: "HttpEvent", dataVersion: "1.0", data: "{}");

    // Instead of TopicEndpointUri and TopicKeySetting, use Connection
    [FunctionName("WithConnectionOnly")]
    [return: EventGrid(Connection = "EventGridConnectionAppSetting")]
    public static EventGridEvent WithConnectionOnly(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "event/{subject}")] HttpRequest req, string subject,
    ILogger log) => new EventGridEvent(subject: subject, eventType: "HttpEvent", dataVersion: "1.0", data: "{}");

    [FunctionName("ReturnEventGridEvent")]
    [return: EventGrid(TopicEndpointUri = "EventGridTopicUriAppSetting", TopicKeySetting = "EventGridTopicKeyAppSetting")]
    public static async Task<EventGridEvent> ReturnEventGridEvent(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "event/{subject}")] HttpRequest req, string subject,
    ILogger log)
    {
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var eventGridEvent = new EventGridEvent(subject: subject, eventType: "HttpEvent", dataVersion: "1.0", data: requestBody);
        log.LogInformation($"Event sent: {subject}\n{requestBody}");
        return eventGridEvent;
    }

    [FunctionName("CollectMultipleEventGridEvents")]
    public static async Task<IActionResult> CollectMultipleEventGridEvents(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
        [EventGrid(TopicEndpointUri = "EventGridEndpoint", TopicKeySetting = "EventGridKey")] IAsyncCollector<EventGridEvent> eventCollector)
    {
        var ev = new EventGridEvent(subject: "IncomingRequest", eventType: "IncomingRequest", dataVersion: "1.0", data: await req.ReadAsStringAsync());
        await eventCollector.AddAsync(ev);
        return new OkResult();
    }

    [FunctionName("CollectMultipleCloudEvents")]
    public static async Task<IActionResult> CollectMultipleCloudEvents(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
        [EventGrid(TopicEndpointUri = "EventGridEndpoint", TopicKeySetting = "EventGridKey")] IAsyncCollector<CloudEvent> eventCollector)
    {
        var ev = new CloudEvent(source: "IncomingRequest", type: "IncomingRequest", jsonSerializableData: await req.ReadAsStringAsync());
        await eventCollector.AddAsync(ev);
        return new OkResult();
    }

    [FunctionName("GetSingleEventGridEvent")]
    public static IActionResult GetSingleEventGridEvent(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
        [EventGrid(TopicEndpointUri = "EventGridEndpoint", TopicKeySetting = "EventGridKey")] out EventGridEvent ev)
    {
        ev = new EventGridEvent(subject: "IncomingRequest", eventType: "IncomingRequest", dataVersion: "1.0", data: "Data");
        return new OkResult();
    }

    [FunctionName("GetSingleCloudEvent")]
    public static IActionResult GetSingleCloudEvent(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
        [EventGrid(TopicEndpointUri = "EventGridEndpoint", TopicKeySetting = "EventGridKey")] out CloudEvent ev)
    {
        ev = new CloudEvent(source: "IncomingRequest", type: "IncomingRequest", jsonSerializableData: "Data");
        return new OkResult();
    }
}
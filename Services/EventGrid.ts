type EventGridEvent = {
  // Full resource path to the event source.
  // If not included, Event Grid stamps onto the event.
  // If included, it must match the Event Grid topic Azure Resource Manager ID exactly.
  topic?: string;
  // Publisher-defined path to the event subject.
  subject: string;
  // One of the registered event types for this event source.
  eventType: string;
  // The time the event is generated based on the provider's UTC time.
  eventTime: string;
  // Unique identifier for the event.
  id: string;
  // Event data specific to the resource provider.
  data?: {
    // Object unique to each publisher.
    // Place your properties specific to the resource provider here.
  };
  // The schema version of the data object. The publisher defines the schema version.
  // If not included, it is stamped with an empty value.
  dataVersion?: string;
  // The schema version of the event metadata. Event Grid defines the schema of the top-level properties.
  // If not included, Event Grid will stamp onto the event.
  // If included, must match the metadataVersion exactly (currently, only 1)
  metadataVersion?: string;
};

interface CloudEvent {
  // Identifies the event. Producers must ensure it's unique. Consumers can assume same source+id means duplicates.
  id: string;

  // Identifies the context in which an event happened.
  // Syntax defined by the producer, preferably an absolute URI
  source: string;

  // The version of the CloudEvents specification used. Compliant producers MUST use value "1.0".
  specversion: string;

  // Describes the type of event related to the originating occurrence.
  // Should be prefixed with a reverse-DNS name.
  type: string;

  subject?: string; // Required in EventSchema, but optional here

  // eventType is now "type"
  // eventTime is now "time" and is optional

  // ...
}
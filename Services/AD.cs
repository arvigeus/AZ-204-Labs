// You'll need to provide the ClientID and Secret when you're setting up an application to authenticate against Azure AD
// When registering, you need to setup identity provider (ex. Microsoft) and then click "Add"

// Ensure AzureEventSourceListener is in scope and active while using the client library for log collection.
// Create it as a top-level member of the class using the Event Hubs client.
// using AzureEventSourceListener listener = AzureEventSourceListener.CreateConsoleLogger();

// DefaultAzureCredentialOptions options = new DefaultAzureCredentialOptions
// {
//     Diagnostics =
//     {
//         LoggedHeaderNames = { "x-ms-request-id" },
//         LoggedQueryParameters = { "api-version" },
//         IsAccountIdentifierLoggingEnabled = true, // enable logging of sensitive information
//         IsLoggingContentEnabled = true // log details about the account that was used to attempt authentication and authorization
//     }
// };
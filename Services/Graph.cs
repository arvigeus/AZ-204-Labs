using System.Net.Http.Headers;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;

namespace Services;

// Connectors: deliver external to Graph
// Data Connect: deliver to other Azure Services

// Headers always returned: request-id

// Auth with Bearer

// Full set of HTTP operations

// Pagination via @odata.nextLink
// Metadata: https://graph.microsoft.com/v1.0/$metadata
// My photo: https://graph.microsoft.com/v1.0/me/photo/$value
// My photo metadata: https://graph.microsoft.com/v1.0/me/photo/
// Filter: ?filter=<name> eq '<value>'
// Limit: ?top=5

class GraphService
{
    async Task MSAL()
    {
        var authority = "https://login.microsoftonline.com/" + "tenantId";
        var scopes = new[] { "https://graph.microsoft.com/.default" };

        var app = ConfidentialClientApplicationBuilder.Create("clientId")
            .WithAuthority(authority)
            .WithClientSecret("clientSecret")
            .Build();

        var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();

        var httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

        var response = await httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
    }

    async Task SDK()
    {
        var scopes = new[] { "User.Read" };

        // Multi-tenant apps can use "common",
        // single-tenant apps must use the tenant ID from the Azure portal
        var tenantId = "common";

        // Value from app registration
        var clientId = "YOUR_CLIENT_ID";

        var options = new TokenCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
        };

        // Using device code: https://learn.microsoft.com/dotnet/api/azure.identity.devicecodecredential
        var deviceOptions = new DeviceCodeCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
            ClientId = clientId,
            TenantId = tenantId,
            // Callback function that receives the user prompt
            // Prompt contains the generated device code that user must
            // enter during the auth process in the browser
            DeviceCodeCallback = (code, cancellation) =>
            {
                Console.WriteLine(code.Message);
                return Task.FromResult(0);
            },
        };
        var credential = new DeviceCodeCredential(deviceOptions);
        // var credential = new DeviceCodeCredential(callback, tenantId, clientId, options);

        // Using a client certificate: https://learn.microsoft.com/dotnet/api/azure.identity.clientcertificatecredential
        // var clientCertificate = new X509Certificate2("MyCertificate.pfx");
        // var credential = new ClientCertificateCredential(tenantId, clientId, clientCertificate, options);

        // Using a client secret: https://learn.microsoft.com/dotnet/api/azure.identity.clientsecretcredential
        // var credential = new ClientSecretCredential(tenantId, clientId, clientSecret, options);

        // On-behalf-of provider
        // var oboToken = "JWT_TOKEN_TO_EXCHANGE";
        // var onBehalfOfCredential = new OnBehalfOfCredential(tenantId, clientId, clientSecret, oboToken, options);

        var graphClient = new GraphServiceClient(credential, scopes);

        var user = await graphClient.Me.GetAsync();

        var messages = await graphClient.Me.Messages
        .GetAsync(requestConfig =>
        {
            requestConfig.QueryParameters.Select =
                new string[] { "subject", "sender" };
            requestConfig.QueryParameters.Filter =
                "subject eq 'Hello world'";

            requestConfig.Headers.Add(
                "Prefer", @"outlook.timezone=""Pacific Standard Time""");
        });

        var message = await graphClient.Me.Messages["messageId"].GetAsync();

        var newCalendar = await graphClient.Me.Calendars
            .PostAsync(new Calendar { Name = "Volunteer" }); // new

        await graphClient.Teams["teamId"]
            .PatchAsync(new Team { }); // update

        await graphClient.Me.Messages["messageId"]
            .DeleteAsync();
    }
}
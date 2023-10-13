// User requests file via special URL. It directs to nearest POP.
// If not cached, fetched from origin server (Azure or web server).
// Sent to user and cached at POP for faster delivery to others.

// ETag and Last-Modified control cache behavior.
// Content is cached based on TTL, determined by `Cache-Control` header
// - Generalized: 7 days
// - Large files: 1 day
// - Media streaming: one year

// Caching behavior settings:
// - Bypass cache: No caching; ignore origin headers.
// - Override: Use provided duration, except for cache-control: no-cache.
// - Set if missing: Use origin headers or provided duration if absent.

// Purging clears main servers, not browser caches. To update users, rename files or use caching methods.
// Recreating a CDN endpoint also purges content from edge servers. (don't!)

// To ensure users receive the latest version of a file, include a version string in the URL or purge cached content

// Compression (Front Door): specific type, 1kb-8mb; gzip and brotli

// Search: "cdn features" for sku

using Microsoft.Azure.Management.Cdn;
using Microsoft.Azure.Management.Cdn.Models;
using Microsoft.Rest;

class CDNService
{
    // You need to configure Azure Active Directory to provide authentication for the application
    public static void ManageCdnEndpoint(string subscriptionId, TokenCredentials authResult, string resourceGroupName, string profileName, string endpointName, string resourceLocation)
    {
        // Create CDN client
        CdnManagementClient cdn = new CdnManagementClient(authResult) { SubscriptionId = subscriptionId };

        // List all the CDN profiles in this resource group
        var profileList = cdn.Profiles.ListByResourceGroup(resourceGroupName);
        foreach (Profile p in profileList)
        {
            // List all the CDN endpoints on this CDN profile
            var endpointList = cdn.Endpoints.ListByProfile(p.Name, resourceGroupName);
            foreach (Endpoint e in endpointList) { }
        }

        // Create a new CDN profile (check if not exist first!)
        var profileParms = new Profile() { Location = resourceLocation, Sku = new Sku(SkuName.StandardVerizon) };
        cdn.Profiles.Create(resourceGroupName, profileName, profileParms);

        // Create a new CDN endpoint (check if not exist first!)
        var endpoint = new Endpoint()
        {
            Origins = new List<DeepCreatedOrigin>() { new DeepCreatedOrigin("Contoso", "www.contoso.com") },
            IsHttpAllowed = true,
            IsHttpsAllowed = true,
            Location = resourceLocation
        };
        cdn.Endpoints.BeginCreateWithHttpMessagesAsync(resourceGroupName, profileName, endpointName, endpoint);

        // Purge content from the endpoint
        cdn.Endpoints.PurgeContent(resourceGroupName, profileName, endpointName, new List<string>() { "/*" });
    }
}
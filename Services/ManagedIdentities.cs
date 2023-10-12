// Token caching via TokenCachePersistenceOptions()
// - memory (default): Managed identities
// - disk

// Public client applications: User-facing apps without the ability to securely store secrets. They interact with web APIs on the user's behalf.
// Confidential client applications: Server-based apps and daemons that can securely handle secrets. Each instance maintains a unique configuration, including identifiers and secrets.

// Changes to your application object also affect its service principals in the home tenant only.
// Deleting the application also deletes its home tenant service principal,
// but restoring that application object won't recover its service principals.

// Search: identity platform
/*
1. Register in Azure AD	                        ✓	✓	✓
2. Configure app with code sample	            ✕	✓	✕
3. Validate token	                            ID	Access	✕
4. Configure secrets & certificates	            ✓	✓	✓
5. Configure permission & call API of choice	✓	✓	✓
6. Control access (authorization)	            ✓	✓ (add validate-jwt policy to validate the OAuth token)	✕
7. Store token cache	                        ✓	✓	✓
*/

// Check for transitive membership in a list of groups (add checkMemberGroups): 
// POST /me/checkMemberGroups
// POST /users/{id | userPrincipalName}/checkMemberGroups

// - Azure AD B2C: social media or user/pass
// - Azure AD B2B: share apps with external users.
// - Azure AD Application Proxy: secure remote access to on-premises applications.
// - Azure AD Connect: synchronize an AD tenant with an on-premises AD domain.
// - Azure AD Enterprise Application: integrate other applications with Azure AD, including your own apps.

// Search: auth flows
// - Authorization code: code for token
// - Client credentials: Confidential App Secret/Certificate
// - On-behalf-of: existing token to get another
// - Device code: Polls the endpoint until user auth
// - Implicit: Token in URI fragment

// Search: app manifest overview

using Azure.Identity;
using Microsoft.Identity.Client;

class AuthService
{
    // Search: msal dev (2)
    async Task Public()
    {
        IPublicClientApplication app = PublicClientApplicationBuilder.Create("your_client_id")
            .WithAuthority(AzureCloudInstance.AzurePublic, "your_tenant_id")
            .WithRedirectUri("http://localhost")
            .Build();

        var scopes = new[] { "User.Read" };

        AuthenticationResult result = await app.AcquireTokenInteractive(scopes).ExecuteAsync();

        Console.WriteLine($"Token:\n{result.AccessToken}");
    }

    async Task Confidential()
    {
        IConfidentialClientApplication app = ConfidentialClientApplicationBuilder.Create("your_client_id")
            .WithClientSecret("your_client_secret")
            .WithAuthority(new Uri("https://login.microsoftonline.com/your_tenant_id")) // public too
            .Build();

        var scopes = new[] { "https://graph.microsoft.com/.default" };

        AuthenticationResult result = await app.AcquireTokenForClient(scopes).ExecuteAsync();

        Console.WriteLine($"Token:\n{result.AccessToken}");
    }
}

class TokenStorageService
{
    void UseDisk()
    {
        var persistenceOptions = new TokenCachePersistenceOptions
        {
            Name = "my_cache_name", // specify a cache name
            UnsafeAllowUnencryptedStorage = true // opt-in for unencrypted storage
        };

        var credential = new InteractiveBrowserCredential(
            new InteractiveBrowserCredentialOptions { TokenCachePersistenceOptions = persistenceOptions }
        );
    }

    void UseMemory()
    {
        new DefaultAzureCredential();
    }
}
using System.Text;
using Azure.Core;
using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Services;

// Cool: 30
// Archive: 180; offline. Copy or change tier. Min 1 hour for premium. Only for individual blob block.

// Lifecycle:
// - Blob blocks: all
// - Version: all
// - Snapshots: no cool
// - Append: only delete

// Filters:
// - prefixMatch: / at the end to match exactly, otherwise it's treated as startsWith
// - blobIndexMatch: json { name, op, value }

// Version / Snapshots
// Creation: Manual / Auto if enabled
// Immut: Always / Only last version

// Replication: requires change feed and versioning

// All HTTP operations are PUT

// restype=container
// comp=block
// comp=appendblock
// comp=page
// comp=lease
// comp=metadata
// comp=list

// Use OAuth access tokens for authentication
// - Delegation Scope: Use `user_impersonation` to allow applications to perform actions permitted by the user.
// - Resource ID: Use `https://storage.azure.com/` to request tokens.

// Anonymous public read access: If allowed at the storage account level; then depends per container/blob

// Properties and meta: x-ms- and x-ms-meta-

// StorageSharedKeyCredential
// DefaultAzureCredential, ClientSecretCredential

class BlobService
{
    async Task CreateSnapshot()
    {
        var options = new BlobClientOptions();
        var blobClient = new BlobClient(new Uri("..."), options);
        BlobSnapshotInfo snapshotInfo = await blobClient.CreateSnapshotAsync();
        // If you attempt to delete a blob that has snapshots, the operation will fail unless you explicitly specify that you also want to delete the snapshots
        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);
    }
}

class BlobFunctions
{
    // If Connection parameter is not specified in [Blob(Connection=?)], then value of "AzureWebJobsStorage" is used
    // You can set it from Azure portal > Configurations > AzureWebJobsStorage
    // or local.settings.json > Values.AzureWebJobsStorage

    // Login
    // Storage Account Key: new StorageSharedKeyCredential(accountName, "<account-key>");
    // AD Login: new DefaultAzureCredential();
    // App registration: new ClientSecretCredential("<tenant-id>", "<client-id>", "<client-secret>");

    // BlobSasBuilder uses DateTimeOffset instead of DateTime

    private static BlobServiceClient GetBlobServiceClient(HttpRequest req, BlobClientOptions? options = null)
    {
        if (req.Headers.TryGetValue("AccountName", out var accountName))
        {
            TokenCredential credential;
            if (req.Headers.TryGetValue("ManagedIdentityId", out var managedIdentityId))
                credential = new ManagedIdentityCredential(managedIdentityId);
            else
                credential = new DefaultAzureCredential();
            return new BlobServiceClient(new Uri($"https://${accountName}.blob.core.windows.net"), credential, options);
        }

        var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage") ?? throw new Exception();
        return new BlobServiceClient(connectionString, options);
    }

    private static BlobContainerClient GetBlobContainerClient(HttpRequest req, string containerName, BlobClientOptions? options = null)
    {
        if (req.Headers.TryGetValue("AccountName", out var accountName))
        {
            TokenCredential credential;
            if (req.Headers.TryGetValue("ManagedIdentityId", out var managedIdentityId))
                credential = new ManagedIdentityCredential(managedIdentityId);
            else
                credential = new DefaultAzureCredential();
            return new BlobContainerClient(new Uri($"https://${accountName}.blob.core.windows.net/{containerName}"), credential, options);
        }

        var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage") ?? throw new Exception();
        return new BlobContainerClient(connectionString, containerName, options);
    }

    [FunctionName("ListContainers")]
    public static async Task<IActionResult> ListContainers(
     [HttpTrigger(AuthorizationLevel.Function, "get", Route = "containers")] HttpRequest req,
     ILogger log)
    {
        var containerClient = GetBlobServiceClient(req);

        if (req.Query.ContainsKey("metadata"))
        {
            var metadata = new List<IDictionary<string, string>>();
            await foreach (var container in containerClient.GetBlobContainersAsync())
                metadata.Add(container.Properties.Metadata);
            return new OkObjectResult(metadata);
        }

        if (req.Query.ContainsKey("properties"))
        {
            var properties = new List<BlobContainerProperties>();
            await foreach (var container in containerClient.GetBlobContainersAsync())
                properties.Add(container.Properties);
            return new OkObjectResult(properties);
        }

        var names = new List<string>();
        await foreach (var container in containerClient.GetBlobContainersAsync())
            names.Add(container.Name);
        return new OkObjectResult(names);
    }

    [FunctionName("ListBlobs")]
    public static async Task<IActionResult> ListBlobs(
     [HttpTrigger(AuthorizationLevel.Function, "get", Route = "containers/{name}")] HttpRequest req, string name,
     [Blob("{name}", FileAccess.Read)] BlobContainerClient containerClient,
     ILogger log)
    {
        if (containerClient == null)
        {
            log.LogError($"Container {name} not found!");
            return new NotFoundResult();
        }

        var items = new List<string>();
        await foreach (var blob in containerClient.GetBlobsAsync())
            items.Add($"{blob.Name}");

        return new OkObjectResult(items);
    }

    [FunctionName("CreateContainer")]
    public static async Task<IActionResult> CreateContainer(
     [HttpTrigger(AuthorizationLevel.Function, "post", Route = "containers/{name}")] HttpRequest req, string name,
     ILogger log)
    {
        var containerClient = GetBlobContainerClient(req, name);
        await containerClient.CreateIfNotExistsAsync();

        if (req.Query.ContainsKey("metadata"))
        {
            var metadata = new Dictionary<string, string>();
            containerClient.SetMetadata(metadata);
        }
        var properties = containerClient.GetProperties();

        return new OkObjectResult(properties);
    }

    [FunctionName("GetBlob")]
    public static IActionResult GetBlob(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "containers/{container}/{name}")] HttpRequest req, string container, string name,
        [Blob("{container}/{name}", FileAccess.Read/*, Connection = "AzureWebJobsStorage" */)] Stream blob,
        ILogger log
    )
    {
        if (blob == null)
        {
            log.LogError($"Blob /{container}/{name} not found!");
            return new NotFoundResult();
        }

        using StreamReader reader = new StreamReader(blob);
        string content = reader.ReadToEnd();
        log.LogInformation($"Blob Content: {content}");
        return new OkObjectResult(content);
    }

    // Azure Functions runtime will automatically create the blob in target container for you when you try to write to it
    [FunctionName("UploadBlob")]
    public static async Task<IActionResult> UploadBlob(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "containers/{container}/{name}")] HttpRequest req, string container, string name,
        [Blob("{container}/{name}", FileAccess.Write)] Stream blob,
        ILogger log
    )
    {
        var content = await new StreamReader(req.Body).ReadToEndAsync();
        using StreamWriter writer = new StreamWriter(blob);
        writer.Write(content);
        return new OkObjectResult(content);
    }

    [FunctionName("SyncAccounts")]
    public static async Task<IActionResult> SyncAccounts(
        [HttpTrigger(AuthorizationLevel.Admin, "get", "post", Route = "sync")] HttpRequest req,
        ILogger log
    )
    {
        var sourceName = req.Query["source"];
        var tatgetName = req.Query["tatget"];

        TokenCredential credential;
        if (req.Headers.TryGetValue("ManagedIdentityId", out var managedIdentityId))
            credential = new ManagedIdentityCredential(managedIdentityId);
        else
            credential = new DefaultAzureCredential();

        var source = new BlobServiceClient(new Uri($"https://${sourceName}.blob.core.windows.net"), credential);
        var target = new BlobServiceClient(new Uri($"https://${tatgetName}.blob.core.windows.net"), credential);

        await foreach (var sourceContainer in source.GetBlobContainersAsync())
        {
            var targetContainer = target.GetBlobContainerClient(sourceContainer.Name);
            await targetContainer.CreateIfNotExistsAsync();

            var sourceBlobContainerClient = source.GetBlobContainerClient(sourceContainer.Name);

            await foreach (var blobItem in sourceBlobContainerClient.GetBlobsAsync())
            {
                log.LogInformation($"Blob name: {blobItem.Name}");

                var sourceBlobClient = sourceBlobContainerClient.GetBlobClient(blobItem.Name);
                var targetBlobClient = targetContainer.GetBlobClient(blobItem.Name);

                // Sync blob to target container
                var sourceBlobUri = sourceBlobClient.Uri;
                await targetBlobClient.StartCopyFromUriAsync(sourceBlobUri);
            }
        }

        return new OkResult();
    }

    [FunctionName("CreateUserDelegatedSas")]
    public static async Task<IActionResult> CreateUserDelegatedSas(
        [HttpTrigger(AuthorizationLevel.Admin, "get", Route = "sas/user/{container}/{name}")] HttpRequest req, string container, string name,
        ILogger log
    )
    {
        var accountName = req.Headers["AccountName"];
        var credential = new DefaultAzureCredential();
        var serviceClient = new BlobServiceClient(new Uri($"https://${accountName}.blob.core.windows.net"), credential);
        var blobClient = serviceClient.GetBlobContainerClient(container).GetBlobClient(name);

        var sasBuilder = new BlobSasBuilder()
        {
            BlobContainerName = container,
            BlobName = name,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow,
            ExpiresOn = DateTimeOffset.UtcNow.AddDays(1)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Write);

        UserDelegationKey userDelegationKey = await serviceClient.GetUserDelegationKeyAsync(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(1));
        // Add the SAS token to the blob URI
        BlobUriBuilder uriBuilder = new BlobUriBuilder(blobClient.Uri)
        {
            // Specify the user delegation key
            Sas = sasBuilder.ToSasQueryParameters(userDelegationKey, serviceClient.AccountName)
        };

        return new OkObjectResult(uriBuilder.ToUri().ToString());
    }

    [FunctionName("CreateServiceContainerSas")]
    public static IActionResult CreateServiceContainerSas(
        [HttpTrigger(AuthorizationLevel.Admin, "get", Route = "sas/service/{container}/{name}")] HttpRequest req, string container,
        ILogger log
    )
    {
        var accountName = req.Headers["AccountName"];
        var accountKey = req.Headers["AccountKey"];
        var credential = new StorageSharedKeyCredential(accountName, accountKey);
        var containerClient = new BlobContainerClient(new Uri($"https://${accountName}.blob.core.windows.net/{container}"), credential);

        var sasBuilder = new BlobSasBuilder()
        {
            BlobContainerName = container,
            Resource = "c",
            StartsOn = DateTimeOffset.UtcNow,
            ExpiresOn = DateTimeOffset.UtcNow.AddDays(1)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Write);

        var serviceSasUri = containerClient.GenerateSasUri(sasBuilder);

        return new OkObjectResult(serviceSasUri.ToString());
    }

    [FunctionName("CreateServiceBlobSas")]
    public static IActionResult CreateServiceBlobSas(
        [HttpTrigger(AuthorizationLevel.Admin, "get", Route = "sas/service/{container}/{name}")] HttpRequest req, string container, string name,
        ILogger log
    )
    {
        var accountName = req.Headers["AccountName"];
        var accountKey = req.Headers["AccountKey"];
        var credential = new StorageSharedKeyCredential(accountName, accountKey);
        var containerClient = new BlobContainerClient(new Uri($"https://${accountName}.blob.core.windows.net/{container}"), credential);
        var blobClient = containerClient.GetBlobClient(name);

        var sasBuilder = new BlobSasBuilder()
        {
            BlobContainerName = container,
            BlobName = name,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow,
            ExpiresOn = DateTimeOffset.UtcNow.AddDays(1)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Write);

        var serviceSasUri = blobClient.GenerateSasUri(sasBuilder);

        return new OkObjectResult(serviceSasUri.ToString());
    }

    [FunctionName("CreateAccountSas")]
    public static IActionResult CreateAccountSas(
        [HttpTrigger(AuthorizationLevel.Admin, "get", Route = "sas/account")] HttpRequest req,
        ILogger log
    )
    {
        var accountName = req.Headers["AccountName"];
        var accountKey = req.Headers["AccountKey"];
        var credential = new StorageSharedKeyCredential(accountName, accountKey);

        var sasBuilder = new AccountSasBuilder()
        {
            Services = AccountSasServices.Blobs,
            ResourceTypes = AccountSasResourceTypes.Service,
            ExpiresOn = DateTimeOffset.UtcNow.AddDays(1),
            Protocol = SasProtocol.Https
        };
        sasBuilder.SetPermissions(AccountSasPermissions.Read | AccountSasPermissions.Write);

        var sasToken = sasBuilder.ToSasQueryParameters(credential).ToString();
        var blobServiceURI = $"https://{accountName}.blob.core.windows.net?{sasToken}";

        return new OkObjectResult(blobServiceURI);
    }

    [FunctionName("CreateSharedAccessContainerPolicy")]
    public static IActionResult CreateSharedAccessContainerPolicy(
        [HttpTrigger(AuthorizationLevel.Admin, "get", Route = "sap/container/{container}")] HttpRequest req, string container,
        [Blob("{name}", FileAccess.Write)] BlobContainerClient containerClient,
        ILogger log
    )
    {
        var identifier = new BlobSignedIdentifier()
        {
            Id = Guid.NewGuid().ToString(),
            AccessPolicy = new BlobAccessPolicy()
            {
                ExpiresOn = DateTimeOffset.UtcNow.AddDays(1),
                Permissions = "r"
            }
        };
        containerClient.SetAccessPolicy(permissions: new BlobSignedIdentifier[] { identifier });

        return new OkResult();
    }

    [FunctionName("CreateLease")]
    public static async Task<IActionResult> CreateLease(
        [HttpTrigger(AuthorizationLevel.Admin, "get", Route = "sap/container/{container}/{name}")] HttpRequest req, string container, string name,
        [Blob("{container}", FileAccess.Write)] BlobContainerClient containerClient,
        ILogger log
    )
    {
        var leaseId = Guid.NewGuid().ToString();
        var blobLeaseClient = containerClient.GetBlobClient(name).GetBlobLeaseClient(leaseId);
        await blobLeaseClient.AcquireAsync(TimeSpan.FromHours(1));

        return new OkObjectResult(leaseId);
    }

    [FunctionName("AccessLeasedBlob")]
    public static IActionResult AccessLeasedBlob(
        [HttpTrigger(AuthorizationLevel.Admin, "get", Route = "sap/container/{container}/{name}")] HttpRequest req, string container, string name,
        [Blob("{container}", FileAccess.Write)] BlobContainerClient containerClient,
        ILogger log
    )
    {
        var leaseId = req.Headers["LeaseId"];
        var metadata = new Dictionary<string, string>();
        var options = new BlobRequestConditions() { LeaseId = leaseId };
        var blobClient = containerClient.GetBlobClient(name);
        blobClient.SetMetadata(metadata, options);

        return new OkResult();
    }

    [FunctionName("BlobTrigger")]
    public static async Task<IActionResult> RunBlob(
        [BlobTrigger("{container}/{name}.conf")] string myBlob, string container, string name,
        [Blob("{container}/events.log", FileAccess.Write)] AppendBlobClient blobClient,
        ILogger log)
    {
        var logMessage = $"{DateTime.UtcNow}: {container}/{name}.conf changed \n Data: {myBlob}\n";
        using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(logMessage));
        await blobClient.AppendBlockAsync(stream);
        return new OkResult();
    }
}


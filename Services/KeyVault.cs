// Perfect Forward Secrecy (PFS): protects connections between customer and cloud services by unique keys

// Auth
// PUT https://<your-key-vault-name>.vault.azure.net/keys/<your-key-name>?api-version=7.2 HTTP/1.1
// Authorization: Bearer <access_token> # token obtained from Azure Active Directory

using System.Text;
using Azure.Identity;
using Azure.Messaging.EventGrid;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

class KeyVaultService
{
    async Task Crypt()
    {
        var vaultUrl = "https://<Your-Key-Vault-Name>.vault.azure.net/";

        // Fetching a secret
        var secretClient = new SecretClient(vaultUri: new Uri(vaultUrl), credential: new DefaultAzureCredential());
        KeyVaultSecret secret = await secretClient.GetSecretAsync("YourSecretName");
        Console.WriteLine($"Fetched Secret: {secret.Value}");

        var keyClient = new KeyClient(vaultUri: new Uri(vaultUrl), credential: new DefaultAzureCredential());
        // Creating a new key
        KeyVaultKey key = await keyClient.GetKeyAsync("YourKeyName");
        // Encrypting and decrypting data using the key via CryptographyClient
        CryptographyClient cryptoClient = keyClient.GetCryptographyClient(key.Name, key.Properties.Version);
        EncryptResult encryptResult = cryptoClient.Encrypt(EncryptionAlgorithm.RsaOaep, Encoding.UTF8.GetBytes("plaintext"));
        DecryptResult decryptResult = cryptoClient.Decrypt(EncryptionAlgorithm.RsaOaep, encryptResult.Ciphertext);
    }

    async Task Cert()
    {
        var client = new CertificateClient(new Uri("https://<Your-Key-Vault-Name>.vault.azure.net"), new DefaultAzureCredential());

        // Create certificate
        var operation = await client.StartCreateCertificateAsync("certificateName", CertificatePolicy.Default);
        await operation.WaitForCompletionAsync();

        // Retrieve
        var certificate = await client.GetCertificateAsync("certificateName");
    }
}

class KeyVaultFunctions
{
    // Portal > All Services > Key Vaults > key vault > Events > Event Grid Subscriptions > + Event Subscription
    [FunctionName("KeyVaultMonitoring")]
    public static async Task<IActionResult> Run(
    [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
    {
        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var eventGridEvent = EventGridEvent.Parse(new BinaryData(requestBody));

        switch (eventGridEvent.EventType)
        {
            case SystemEventNames.KeyVaultCertificateNewVersionCreated:
            case SystemEventNames.KeyVaultSecretNewVersionCreated:
                log.LogInformation($"New Key Vault secret/certificate version created event. Data: {eventGridEvent.Data}"); break;
            case SystemEventNames.KeyVaultKeyNewVersionCreated:
                log.LogInformation($"New Key Vault key version created event. Data: {eventGridEvent.Data}"); break;
            default:
                log.LogInformation($"Event Grid Event of type {eventGridEvent.EventType} occurred, but it's not processed."); break;
        }

        return new OkResult();
    }
}
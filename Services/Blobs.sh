az storage account create
    --name # valid DNS name, 3-24 chars
    --resource-group

    # Pricing tiers (<Type>_<Redundancy>)
    # Changing type: Copy to another account.
    # Changing redundancy: Instantly applied
    [--sku {Standard_GRS, Standard_GZRS, Standard_LRS, Standard_RAGRS, Standard_ZRS, Standard_RAGZRS, Premium_LRS, Premium_ZRS}]
    # Type 🧊:
    # - Standard: ⏺️⭐
    # - Premium: ⚡💲 (SSD). ⭐: using smaller objects
    # Redundancy:
    # - LRS: 🏷️, ❌: 🙋‍♂️.
    #   ⭐: your application can reconstruct lost data, requires regional replication (perhaps due to governance reasons), or uses Azure unmanaged disks.
    # - ZRS: Data write operations are confirmed successful once all the available zones have received the data. This even includes zones that are temporarily unavailable.
    #   ⭐: 🙋‍♂️, regional data replication, Azure Files workloads.
    # - GRS: LRS + async copy to a secondary region.
    # - GZRS: ZRS + async copy to a secondary region. 🦺
    # Read Access (RA): 🙋‍♂️ Allow read-only from `https://{accountName}-secondary.<url>`
    # Failover: manually initialized, swaps primary and secondary regions.
    # - C#: BlobClientOptions.GeoRedundantSecondaryUri (will not attempt again if 404).
    # - Alt: Copy data.
    # - ❌: Azure Files, BlockBlobStorage

    [--access-tier {Cool, Hot, Premium}] # Premium is inherited by SKU

    [--kind {BlobStorage, BlockBlobStorage, FileStorage, Storage, StorageV2}]
    # - BlobStorage: Simple blob-only scenarios.
    # - BlockBlobStorage: ⚡💎
    # - FileStorage: High-scale or high IOPS file shares. 💎
    # - Storage (General-purpose v1): Legacy. ⭐: classic deployment model or 🏋🏿 apps
    # - StorageV2: ⏺️⭐

    [--dns-endpoint-type {AzureDnsZone, Standard}] # Requires storage-preview extension
    # In one subscription, you can have accounts with both
    # - Standard: 250 accounts (500 with quota increase)
    # - AzureDnsZone: 5000 accounts
    # https://<storage-account>.z[00-50].<storage-service>.core.windows.net
    # Retrieve endpoints: GET https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{accountName}?api-version=2022-09-01

    [--enable-hierarchical-namespace {false, true}]
    # Filesystem semantics. StorageV2 only. ❌ failover

az storage container create
    --name # Valid lowercase DNS name (3-63) with no double dashes
    [--resource-group]
    [--metadata]
    [--public-access {blob, container, off}]

az storage account management-policy create \
    #--account-name "<storage-account>" \
    #--resource-group $resourceGroup
    --policy @policy.json

az storage account <create/update>
    [--encryption-key-source {Microsoft.Keyvault, Microsoft.Storage}]
    [--encryption-services {blob, file, queue, table}] # queue / table with customer-managed keys = 💲
    [--encryption-key-type-for-queue {Account, Service}]
    [--encryption-key-type-for-table {Account, Service}]
    # When using Microsoft.Keyvault:
    #   [--encryption-key-name]
    #   [--encryption-key-vault] # URL
    #   [--encryption-key-version]
    # 🧊 Optionally encrypt infrastructure with separate Microsoft managed key. StorageV2 or BlockBlobStorage only.
    [--require-infrastructure-encryption {false, true}] # false

az storage account encryption-scope create
    --account-name
    --name "<scope-name>"
    [--key-source {Microsoft.KeyVault, Microsoft.Storage}] # Same rules like encryption at account level
    [--key-uri] # For KeyVault
    [--require-infrastructure-encryption {false, true}] # Inherited from storage account level, if set

# Optional
az storage container create
    --default-encryption-scope "<scope-name>"
    --prevent-encryption-scope-override true # force all blobs in a container to use the container's default scope

az storage <type> <operation>
    --encryption-scope "<scope-name>" # if not set, inherited from container or storage account
    # EncryptionScope property for BlobOptions in C#

az storage blob <command>
    # Authenticate:
    ## By Storage Account Key
    --account-key # az storage account keys list -g $resourcegroup -n $accountname --query '[0].value' -o tsv
    ## By AD Login
    --auth-mode login # Use credentials from az login
    ## By Connection String
    --connection-string
    ## By SAS token
    --sas-token

    # Select target blob
    ## By name
    [--blob-endpoint] # https://<storage-account>.blob.core.windows.net
    [--account-name] # When using storage account key or a SAS token
    --container-name
    --name # Case sensitive, cannot end with dot (.) or dash (-)
    ## By URL
    --blob-url "https://<storage-account>.blob.core.windows.net/<container>/<blob>?<SAS>" # Use <SAS> only if unauthenticated.

    # <command>
    ## upload
    --file "/path/to/file" # for file uploads
    --data "some data" # for text uploads
    ## copy start-batch
    ## use -source-<prop> and --destination-<prop>

# Example
az storage blob upload --file /path/to/file --container mycontainer --name MyBlob
az storage container list --account-name $storageaccountname # get containers
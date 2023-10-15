# Best practices:
# - Always use HTTPS.
# - Use Azure Monitor and Azure Storage logs to monitor the application.
# - Use user delegation SAS wherever possible.
# - Set your expiration time to the smallest useful value.
# - Only grant the access that's required.
# - Create a middle-tier service to manage users and their access to storage when there's an unacceptable risk of using a SAS.

# All need --account-name
# Service and Account Level SAS need --account-key
# Service SAS needs --resource-types
# User Level SAS: --auth-mode login

az storage container policy create \
    --name <stored access policy identifier> \
    --container-name <container name> \
    --start <start time UTC datetime> \
    --expiry <expiry time UTC datetime> \
    --permissions <(a)dd, (c)reate, (d)elete, (l)ist, (r)ead, or (w)rite> \
    --account-key <storage account key> \
    --account-name <storage account name>

az role assignment create \
 --role "Storage Blob Data Contributor" \
 --assignee <email> \
 --scope "/subscriptions/<subscription>/resourceGroups/<resource-group>/providers/Microsoft.Storage/storageAccounts/<storage-account>"

# Generate a user delegation SAS for a container
az storage container generate-sas \
 --account-name <storage-account> \
 --name <container> \
 --permissions acdlrw \
 --expiry <date-time> \
 --auth-mode login \
 --as-user

# Generate a user delegation SAS for a blob
az storage blob generate-sas \
 --account-name <storage-account> \
 --container-name <container> \
 --name <blob> \
 --permissions acdrw \
 --expiry <date-time> \
 --auth-mode login \
 --as-user \
 --full-uri

# Revoke all user delegation keys for the storage account
az storage account revoke-delegation-keys \
 --name <storage-account> \
 --resource-group $resourceGroup
````
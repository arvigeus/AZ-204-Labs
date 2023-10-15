# No scaling

# Login to manage resources
az login

# Create a resource group
az group create --name $resourceGroup --location eastus

# (Optional)

# Deployment
##
## NOTE: If using managed identities with ACR, you'll also need --asign-identity param
## or az container identity assign --identities $identityName --resource-group $resourceGroup --name $containerName
##
## From image - simple scenarios
###
### Azure File share: https://learn.microsoft.com/en-us/azure/container-instances/container-instances-volume-azure-files
### Can only be mounted to Linux containers running as root!
### --os-type Linux
### --azure-file-volume-account-name # Azure File Share requires existing storage account and account key
### --azure-file-volume-account-key
### --azure-file-volume-mount-path
### --azure-file-volume-share-name
### NOTE: No direct integration Blob Storage because it lacks SMB support
###
### Public DNS name (must be unique) - accessible from $dnsLabel.<region>.azurecontainer.io
### --dns-name-label $dnsLabel
### --ip-address public
###
### [--restart-policy {Always, Never, OnFailure}] # Default: Always. Never if you only want to run once. Status when stopped: Terminated
###
### Environment variables: https://learn.microsoft.com/en-us/azure/container-instances/container-instances-environment-variables
#### NOTE: Format can be 'key'='value', key=value, 'key=value'
### --environment-variables # ex: 'PUBLIC_ENV_VAR'='my-exposed-value'
### --secure-environment-variables # ex: 'SECRET_ENV_VAR'='my-secret-value' - not visible in your container's properties
###
### Mount secret volumes: https://learn.microsoft.com/en-us/azure/container-instances/container-instances-volume-secret
### --secrets mysecret1="My first secret FOO" mysecret2="My second secret BAR"
### --secrets-mount-path /mnt/secrets
### NB: Restricted to Linux containers
### NOTE: This creates mysecret1 and mysecret2 files in /mnt/secrets with value the content of the secret
###
az container create --name $containerName --image $imageName:$tag --resource-group $resourceGroup
##
## From YAML file - deployment includes only container instances
### Same options as from simple deployment, but in a YAML file. Includes container groups.
az container create --name $containerName --file deploy.yml --resource-group $resourceGroup
##
## ARM template - deploy additional Azure service resources (for example, an Azure Files share)
### No example, but it's good to know this fact

# Verify container is running
az container show --name $containerName --resource-group $resourceGroup --query "{FQDN:ipAddress.fqdn,ProvisioningState:provisioningState}" --out table

# Logging
az container attach # Connects your local console to a container's output and error streams in real time (example: to debug startup issue).
az container logs # Displays logs (when no real time monitoring is needed)
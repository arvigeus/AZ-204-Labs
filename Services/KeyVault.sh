az keyvault set-policy --name <your-key-vault-name> --upn user@domain.com \
    # Using Customer-Managed Keys for Encryption: Standard tier app config, soft-delete + purge protection
    --key-permissions <key-permissions> # To access: GET, WRAP, UNWRAP \
    --secret-permissions <secret-permissions> \
    --certificate-permissions <certificate-permissions> # delete get list create purge



az login

# A resource group is a logical container into which Azure resources are deployed and managed.
az group create --name $resourceGroup --location eastus

# Create a key vault in the same region and tenant as the VMs to be encrypted.
# The key vault will be used to control and manage disk encryption keys and secrets.
az keyvault create --name "<keyvault-id>" --resource-group $resourceGroup --location "eastus"

# Update the key vault's advanced access policies
az keyvault update --name "<keyvault-id>" --resource-group $resourceGroup --enabled-for-disk-encryption "true"
# Enables the Microsoft.Compute resource provider to retrieve secrets from this key vault when this key vault is referenced in resource creation, for example when creating a virtual machine.
az keyvault update --name "<keyvault-id>" --resource-group $resourceGroup --enabled-for-deployment "true"
# Allow Resource Manager to retrieve secrets from the vault.
az keyvault update --name "<keyvault-id>" --resource-group $resourceGroup --enabled-for-template-deployment "true"

# This step is optional. When a key encryption key (KEK) is specified, Azure Disk Encryption uses that key to wrap the encryption secrets before writing to Key Vault.
az keyvault key create --name "myKEK" --vault-name "<keyvault-id>" --kty RSA --size 4096

# Enable disk encryption:
## Optionally use KEK by name
az vm encryption enable -g $resourceGroup --name "myVM" --disk-encryption-keyvault "<keyvault-id>" --key-encryption-key "myKEK"
## Optionally use KEK by url
## Obtain <kek-url>
## az keyvault key show --vault-name "<keyvault-id>" --name "myKEK" --query "key.kid"
## az vm encryption enable -g $resourceGroup --name "MyVM" --disk-encryption-keyvault "<keyvault-id>" --key-encryption-key-url <kek-url> --volume-type All
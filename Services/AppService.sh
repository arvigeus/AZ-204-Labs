## Deploying apps
# - Create resource group: `az group create`
# - Create App Service plan: `az appservice plan create --location $location`
# - Create web app: `az webapp create --runtime "DOTNET|6.0"`
# - (optinal) Use managed identity for ACR:
#    - Assign managed identity to the web app
#    - Assign `AcrPull` role: `az role assignment create --assignee $principalId --scope $registry_resource_id --role "AcrPull"`
#    - Set generic config to `{acrUseManagedIdentityCreds:true}` for system identity and `{acrUserManagedIdentityID:id}` for user identity: `az webapp config set --generic-configurations '<json>'`
# - (optional) Create deployment slot (staging) (Standard+): `az webapp deployment slot create`
# - Deploy app (add `--slot staging` to use deployment slot):
#    - Git: `az webapp deployment source config --repo-url $gitrepo --branch master --manual-integration`
#    - Docker: `az webapp config container set --docker-custom-image-name`
#    - Compose (skip step 3): `az webapp create --multicontainer-config-type compose --multicontainer-config-file $dockerComposeFile`
#    - Local ZIP file: `az webapp deploy --src-path "path/to/zip"`
#    - Remote ZIP file: `az webapp deploy --src-url "<url>"`
# - (optional) Set some settings: `az webapp config appsettings set --settings` (ex: `DEPLOYMENT_BRANCH='main'` for git, `SCM_DO_BUILD_DURING_DEPLOYMENT=true` for build automation)

# Source Settings:
# Key vault:
# - @Microsoft.KeyVault(SecretUri=https://myvault.vault.azure.net/secrets/mysecret/)
# - @Microsoft.KeyVault(VaultName=myvault;SecretName=mysecret)
# App Configuration: @Microsoft.AppConfiguration(Endpoint=https://myAppConfigStore.azconfig.io; Key=myAppConfigKey; Label=myKeysLabel)

# Managed identities for App Service and Azure Functions: App Service / Database and Service Connection / Connect using app identity
# GET {IDENTITY_ENDPOINT}?resource=https://vault.azure.net&api-version=2019-08-01&client_id=XXX
# X-IDENTITY-HEADER: {IDENTITY_HEADER} # Mitigate SSRF attacks

# Authentication flows: App Service / Auth / Built in
# Server vs Client Sign-in: Server uses redirects for authentication, while client uses the provider's SDK and validates with the server.
# Session Management: Post-authentication, the server either sets a cookie or returns a token (client). This is used in subsequent requests for authenticated access.

# TLS mutual authentication: Basic+
# X-ARR-ClientCert header; HttpRequest.ClientCertificate

# Scaling
# - Manual scaling (Basic+) - one time events (example: doing X on this date)
# - Autoscale (Standard+) - for predictable changes of application load, based on schedules (every X days/weeks/months) or resources
# - Automatic scaling (PremiumV2+) - pre-warmed, always-ready; avoid cold start

# Deployment slots
# Best practices: Deploy to staging, then swap slots to warm up instances and eliminate downtime.
# - Swapped: Settings that define the application's _behavior_. Includes connection strings, authentication settings, public certificates, path mappings, CDN, hybrid connections.
# - Not Swapped: Settings that define the application's _environment and security_. They are less about the application itself and more about how it interacts with the external world. Examples: Private certificates, managed identities, publishing endpoints, diagnostic logs settings, CORS.

# The _App Service file system_ option is for temporary debugging purposes, and turns itself off in 12 hours.
# _The Blob_ option is for long-term logging, includes additional information. .Net apps only.
# Linux can only have App loging in blobs, Windows: server too
az webapp log config --application-logging {azureblobstorage, filesystem, off} --name MyWebapp --resource-group $resourceGroup

# Private Health checks: x-ms-auth-internal-token request header must equals the hashed value of WEBSITE_AUTH_ENCRYPTION_KEY

# Local Cache: “WEBSITE_LOCAL_CACHE_OPTION”: “Always”, “WEBSITE_LOCAL_CACHE_SIZEINMB”: “1500”
# Serach: local cache app service

# Move App Service plan by cloning it. Source plan and destination plan must be in the same resource group, geographical region, same OS type, and supports the currently used features.
# New-AzResourceGroup -Name DestinationAzureResourceGroup -Location $destinationLocation
# New-AzAppServicePlan -Location $destinationLocation -ResourceGroupName DestinationAzureResourceGroup -Name DestinationAppServicePlan -Tier Standard
# $srcapp = Get-AzWebApp -Name MyAppService -ResourceGroupName SourceAzureResourceGroup
# $destapp = New-AzWebApp -SourceWebApp $srcapp -AppServicePlan DestinationAppServicePlan -Location $destinationLocation -ResourceGroupName DestinationAzureResourceGroup -Name MyAppService2

# Configuration:
# az webapp config - identities
# az webapp config appsettings
# az webapp config container - docker and compose deployment
# az webapp log config - where to log

# Stream HTTP logs
az webapp log tail --provider http --name $app --resource-group $resourceGroup
# Stream errors
az webapp log tail --filter Error --name $app --resource-group $resourceGroup # filter by word Error
az webapp log tail --only-show-errors --name $app --resource-group $resourceGroup

############################

let "randomIdentifier=$RANDOM*$RANDOM"
location="East US"
resourceGroup="app-service-rg-$randomIdentifier"
tag="deploy-github.sh"
appServicePlan="app-service-plan-$randomIdentifier"
webapp="web-app-$randomIdentifier"
gitrepo="https://github.com/Azure-Samples/dotnet-core-sample"

az group create --name $resourceGroup --location "$location" --tag $tag

az appservice plan create --name $appServicePlan --resource-group $resourceGroup --location $location # --sku B1
# az appservice plan create --name $appServicePlan --resource-group $resourceGroup --sku S1 --is-linux

az webapp create --name $webapp --plan $appServicePlan --runtime "DOTNET|6.0" --resource-group $resourceGroup

# https://learn.microsoft.com/en-us/azure/app-service/scripts/cli-deploy-github
github_deployment() {
    echo "Deploying from GitHub"
    az webapp deployment source config --name $webapp --repo-url $gitrepo --branch master --manual-integration --resource-group $resourceGroup

    # Change deploiment branch to "main"
    # az webapp config appsettings set --name $webapp --settings DEPLOYMENT_BRANCH='main' --resource-group $resourceGroup
}

# https://learn.microsoft.com/en-us/azure/app-service/scripts/cli-deploy-staging-environment
# Use it to avoid locking files
staging_deployment() {
    # Deployment slots require Standard tier, default is Basic (B1)
    az appservice plan update --name $appServicePlan --sku S1 --resource-group $resourceGroup

    echo "Creating a deployment slot"
    az webapp deployment slot create --name $webapp --slot staging --resource-group $resourceGroup

    echo "Deploying to Staging Slot"
    az webapp deployment source config --name $webapp --resource-group $resourceGroup \
      --slot staging \
      --repo-url $gitrepo \
      --branch master --manual-integration \


    echo "Swapping staging slot into production"
    az webapp deployment slot swap --slot staging --name $webapp --resource-group $resourceGroup
}

# https://learn.microsoft.com/en-us/azure/app-service/configure-custom-container?tabs=debian&pivots=container-linux#change-the-docker-image-of-a-custom-container
docker_deployment() {
    # (Optional) Use managed identity: https://learn.microsoft.com/en-us/azure/app-service/configure-custom-container?tabs=debian&pivots=container-linux#change-the-docker-image-of-a-custom-container
    ## Enable the system-assigned managed identity for the web app
    az webapp identity assign --name $webapp --resource-group $resourceGroup
    ## Grant the managed identity permission to access the container registry
    az role assignment create --assignee $principalId --scope $registry_resource_id --role "AcrPull"
    ## Configure your app to use the system managed identity to pull from Azure Container Registry
    az webapp config set --generic-configurations '{"acrUseManagedIdentityCreds": true}' --name $webapp --resource-group $resourceGroup
    ## (OR) Set the user-assigned managed identity ID for your app
    az webapp config set --generic-configurations '{"acrUserManagedIdentityID": "$principalId"}' --name $webapp --resource-group $resourceGroup

    echo "Deploying from DockerHub" # Custom container
    az webapp config container set --name $webapp --resource-group $resourceGroup \
      --docker-custom-image-name <docker-hub-repo>/<image> \
      # Private registry: https://learn.microsoft.com/en-us/azure/app-service/configure-custom-container?tabs=debian&pivots=container-linux#use-an-image-from-a-private-registry
      --docker-registry-server-url <private-repo-url> \
      --docker-registry-server-user <username> \
      --docker-registry-server-password <password>

    # NOTE: Another version of it, using
    # az webapp create --deployment-container-image-name <registry-name>.azurecr.io/$image:$tag
    # https://learn.microsoft.com/en-us/azure/app-service/tutorial-custom-container
}

# https://learn.microsoft.com/en-us/azure/app-service/tutorial-multi-container-app
compose_deployment() {
    echo "Creating webapp with Docker Compose configuration"
    $dockerComposeFile=docker-compose-wordpress.yml
    # Note that az webapp create is different
    az webapp create --resource-group $resourceGroup --plan $appServicePlan --name wordpressApp --multicontainer-config-type compose --multicontainer-config-file $dockerComposeFile

    echo "Setup database"
    az mysql server create --resource-group $resourceGroup --name wordpressDb  --location $location --admin-user adminuser --admin-password letmein --sku-name B_Gen5_1 --version 5.7
    az mysql db create --resource-group $resourceGroup --server-name <mysql-server-name> --name wordpress

    echo "Setting app settings for WordPress"
    az webapp config appsettings set \
      --settings WORDPRESS_DB_HOST="<mysql-server-name>.mysql.database.azure.com" WORDPRESS_DB_USER="adminuser" WORDPRESS_DB_PASSWORD="letmein" WORDPRESS_DB_NAME="wordpress" MYSQL_SSL_CA="BaltimoreCyberTrustroot.crt.pem" \
      --resource-group $resourceGroup \
      --name wordpressApp
}

# https://learn.microsoft.com/en-us/azure/app-service/deploy-zip?tabs=cli
# uses the same Kudu service that powers continuous integration-based deployments
zip_archive() {
  az webapp deploy --src-path "path/to/zip" --name $webapp --resource-group $resourceGroup
  # Zip from url
  # az webapp deploy --src-url "https://storagesample.blob.core.windows.net/sample-container/myapp.zip?sv=2021-10-01&sb&sig=slk22f3UrS823n4kSh8Skjpa7Naj4CG3" --name $webapp --resource-group $resourceGroup

  # (Optional) Enable build automation
  # az webapp config appsettings set --settings SCM_DO_BUILD_DURING_DEPLOYMENT=true --name $webapp --resource-group $resourceGroup
}
# No root, linux/amd64 container images
# auth runs as a sidecar
# State doesn't persist inside a container. Use external cache.
# A webhook can notify Azure Container Apps of a new image in ACR, triggering its automatic deployment.

# Scopes:
# - Revision-scope: Changing properties.template creates a new revision. Example: version, config, scaling.
# - Application-scope: Changies to properties.configuration are applied to all revisions. Example: secrets, mode, ingress, credentials.

# Logs:
# - System Logs (at the container app level)
# - Console Logs (from the `stderr` and `stdout` messages inside container app)

# Upgrade Azure CLI version on the workstation
az upgrade

# Add and upgrade the containerapp extension for managing containerized services
az extension add --name containerapp --upgrade

# Login to Azure
az login

# Register providers for Azure App Services (for hosting APIs) and Azure Operational Insights (for telemetry)
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.OperationalInsights

# Create an environment 'prod' in Azure Container Apps
az containerapp env create --resource-group $resourceGroup --name prod

# Deploy the API service to the 'prod' environment, using the source code from a repository
# https://learn.microsoft.com/en-us/azure/container-apps/quickstart-code-to-cloud
function deploy_repo() {
  az containerapp up \
    --name MyAPI \
    --resource-group $resourceGroup \
    --location eastus \
    --environment prod \
    --context-path ./src \
    --repo myuser/myrepo \
    --ingress 'external'

  # Display the Fully Qualified Domain Name (FQDN) of the app after it's deployed. This is the URL you would use to access your application.
  az containerapp show --name MyAPI --resource-group $resourceGroup --query properties.configuration.ingress.fqdn
}

# Deploy a containerized application in Azure Container Apps, using an existing public Docker image
# https://learn.microsoft.com/en-us/azure/container-apps/get-started
function deploy_image() {
  az containerapp up \
    --name MyContainerApp \
    --resource-group $resourceGroup \
    --environment prod \
    --image mcr.microsoft.com/azuredocs/containerapps-helloworld:latest \
    --target-port 80 \
    --ingress 'external' \ # allows the application to be accessible from the internet.
    # Display the Fully Qualified Domain Name (FQDN) of the app after it's deployed. This is the URL you would use to access your application.
    --query properties.configuration.ingress.fqdn

    # Alt: Deploy from a Docker Image in Azure Container Registry (ACR)
    # --image myAcr.azurecr.io/myimage:latest \
    # --registry-username myAcrUsername \
    # --registry-password myAcrPassword \
}

#######################

function using_secrets() {
    # restart to reflect updates
    
    # Key vault
    az keyvault create --name MyKeyVault --resource-group $resourceGroup --location eastus
    az containerapp create \
      --resource-group $resourceGroup \
      --name queuereader \
      --environment prod \
      --image demos/queuereader:v1 \
      --user-assigned "<USER_ASSIGNED_IDENTITY_ID>" \
      --secrets ""api-key=$API_KEY" queue-connection-string=keyvaultref:<KEY_VAULT_SECRET_URI>,identityref:<USER_ASSIGNED_IDENTITY_ID>" \
      --secret-volume-mount "/mnt/secrets" \ # Mounting in a volume
      # Referencing: `secretref:``
      --env-vars "QueueName=myqueue" "ConnectionString=secretref:queue-connection-string"
}

function scale_by_servicebus() {
    # Custom: annot scale to 0
    az containerapp create \
      --name <CONTAINER_APP_NAME> \
      --resource-group <RESOURCE_GROUP> \
      --environment <ENVIRONMENT_NAME> \
      --image <CONTAINER_IMAGE_LOCATION>
      --min-replicas 0 \
      --max-replicas 5 \
      --secrets "connection-string-secret=<SERVICE_BUS_CONNECTION_STRING>" \
      --scale-rule-name azure-servicebus-queue-rule \
      --scale-rule-type azure-servicebus \
      --scale-rule-metadata "queueName=my-queue" \
                            "namespace=service-bus-namespace" \
                            "messageCount=5" \
        --scale-rule-auth "connection=connection-string-secret" # No secretref because it's not env var
}

function scaling() {
    # Adding or editing scaling rules creates a new revision of the container app
    az containerapp create \
      # Revisions
      # - Single Mode: Old revision stays until new is ready.
      # - Multi Mode: Control lifecycle and traffic via ingress; switches to latest when ready.
      # Labels: Route traffic to specific revisions via unique URLs.
      -revision-mode "Single|Multiple"

      --min-replicas 0 \
      --max-replicas 5 \

      #  HTTP Scaling Rule
      # Based on the number of concurrent HTTP requests to your revision.
      --scale-rule-name http-rule-name \
      --scale-rule-type http \
      --scale-rule-http-concurrency 100

      # TCP Scaling Rule
      # Based on the number of concurrent TCP connections to your revision.
      --scale-rule-name tcp-rule-name \
      --scale-rule-type tcp \
      --scale-rule-tcp-concurrency 100
}

function dapr() {
    # To load components only for the right apps, application scopes are used (or all will be loaded).
    az containerapp create --dapr-enabled
    # Pub/sub: implement event-driven architectures
    # Observability: Sends tracing information to an Application Insights backend.
    # Bindings: Communicate with external systems.
}
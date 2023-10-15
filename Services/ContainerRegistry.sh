# Login to manage resources
az login

# Create a resource group
az group create --name $resourceGroup --location eastus

# Create Azure Container Registry
## https://learn.microsoft.com/en-us/azure/container-registry/container-registry-skus
## --sku {Basic,Standard,Premium} # 10, 100, 500GB; 💎: Concurrent operations, High volumes (⚡), Customer-Managed Key, Content trust for image tag signing, Private link
## Throttling: May happen if you exceed the registry's limits, causing temporary `HTTP 429` errors and requiring retry logic or reducing the request rate.
##
## [--default-action {Allow, Deny}] # 💎: Default action when no rules apply
##
## https://learn.microsoft.com/en-us/azure/container-registry/zone-redundancy
## [--zone-redundancy {Disabled, Enabled}] # 💎: Min 3 separate zones in each enabled region. The environment must include a virtual network (VNET) with an available subnet.
az acr create --resource-group $resourceGroup --name $registryName --sku Standard # ⭐: Production
# NOTE: High numbers of repositories and tags can impact the performance. Periodically delete unused.

# ACR Login: https://learn.microsoft.com/en-us/azure/container-registry/container-registry-authentication
## - Interactive: Individual AD login, Admin Account
## - Unatended / Headless: AD Service Principal, Managed Identity for Azure Resources
## Roles: https://learn.microsoft.com/en-us/azure/container-registry/container-registry-roles?tabs=azure-cli
##
## 1) Individual login with Azure AD: Interactive push/pull by developers, testers.
## az login - provides the token. It has to be renewed every 3 hours
az acr login --name "$registryName" # Token must be renewed every 3 hours.
##
## 2) AD Service Principal: Unattended push/pull in CI/CD pipelines
### Create service principal
#### Method 1: Short version that will setup and return appId and password in JSON format
az ad sp create-for-rbac --name $ServicePrincipalName --role AcrPush,AcrPull,AcrDelete --scopes /subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.ContainerRegistry/registries/$registryName
#### Method 2: Create a service principal and configure roles separately
az ad sp create --id $ServicePrincipalName
az role assignment create --assignee $appId --role AcrPush,AcrPull,AcrDelete --scope /subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.ContainerRegistry/registries/$registryName
az ad sp credential reset --name $appId # for method 2 password is not explicitly created, so we need to create (reset) it
#### Note: Password expires in 1 year.
az acr login --name $registryName --username $appId --password $password
##
## 3) Managed identities
az role assignment create --assignee $managedIdentityId --scope $registryName --role AcrPush,AcrPull,AcrDelete
## Now container instances / apps must use that managed identity to access this ACR (pull or push images)
##
## 4) Admin User: ❌. Interactive push/pull by individual developers.
### The admin account is provided with two passwords, both of which can be regenerated
az acr update -n $registryName --admin-enabled true # this is disabled by default
docker login $registryName.azurecr.io

# Tasks: https://learn.microsoft.com/en-us/azure/container-registry/container-registry-tasks-overview
## [--platform Linux {Linux, Windows}] # Linux supports all architectures (ex: Linux/arm), Windows: only amd64 (ex: Windows/amd64) - arch is optional
##
## - Quick task
az acr build --registry $registryName --image $imageName:$tag . # docker build, docker push
az acr run --registry $registryName --cmd '$registryName/$repository/$imageName:$tag' /dev/null # Run image (last param is source location, optional for non-image building tasks)
##
## - Automatically Triggered Task
### [--<operation>-trigger-enabled true] # CI on commit or pull-request
### [--schedule] # CRON schedule (⭐: OS/framework patching): https://learn.microsoft.com/en-us/azure/container-registry/container-registry-tasks-scheduled
az acr task create --name ciTask --registry $registryName --image $imageName:{{.Run.ID}} --context https://github.com/myuser/myrepo.git --file Dockerfile --git-access-token $GIT_ACCESS_TOKEN
az acr task create --name cmdTask --registry $registryName --cmd mcr.microsoft.com/hello-world --context /dev/null
### az acr task run --name mytask --registry $registryName # manually run task
##
## - Multi-step Task: granular control (build, push, when, cmd defined as steps) - https://learn.microsoft.com/en-us/azure/container-registry/container-registry-tasks-reference-yaml
### NOTE: --file is used for both multi-step task and Dockerfile
az acr run --file multi-step.yaml https://github.com/Azure-Samples/acr-tasks.git
az acr task create --file multi-step.yaml --name ciTask --registry $registryName --image $imageName:{{.Run.ID}} --context https://github.com/myuser/myrepo.git --git-access-token $GIT_ACCESS_TOKEN

# List images and tags
az acr repository list --name $registryName --output table
az acr repository show-tags --name $registryName --repository $repository --output table
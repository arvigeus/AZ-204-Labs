# 1) Create resource group
# 2) Create namespace
# 3) Create queue

az group create --name $resourceGroup --location $location

az servicebus namespace create --name mynamespace --resource-group $resourceGroup --location $location

az servicebus queue create --name myqueue --namespace-name mynamespace --resource-group $resourceGroup

az servicebus queue list --namespace-name mynamespace --resource-group $resourceGroup

az servicebus namespace authorization-rule keys list --name RootManageSharedAccessKey --namespace-name mynamespace --resource-group $resourceGroup --query primaryConnectionString

az servicebus queue delete --name myqueue --namespace-name mynamespace --resource-group $resourceGroup
az servicebus namespace delete --name mynamespace --resource-group $resourceGroup
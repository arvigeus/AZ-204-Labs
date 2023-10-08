# 1) Create resource group
# 2) Create event hub namespace
# 3) Create an Event Hub inside the namespace
# 4) Create consumer group
# 5) (Optional) Enable event hub capture with storage account SAS url and container name
# NOTE: Everything starts with "az eventhubs ..."

# Create a resource group
az group create --name $resourceGroup --location $location

# Create an Event Hubs namespace
# Throughput units are specified here
az eventhubs namespace create --name $eventHubNamespace --sku Standard --location $location --resource-group $resourceGroup

# Get the connection string for a namespace
az eventhubs namespace authorization-rule keys list --namespace-name $eventHubNamespace --name RootManageSharedAccessKey --resource-group $resourceGroup

# Create an Event Hub inside the namespace
# Partition count and retention days are specified here
az eventhubs eventhub create --partition-count 2 --message-retention 1 --name $eventHub --namespace-name $eventHubNamespace --resource-group $resourceGroup

# Create a Consumer Group
az eventhubs eventhub consumer-group create --name MyConsumerGroup --eventhub-name $eventHub --namespace-name $eventHubNamespace --resource-group $resourceGroup

# Capture Event Data (Event Hubs Capture) - Requires Standard+
# Enable capture and specify the storage account and container
az eventhubs eventhub update --enable-capture True --storage-account sasurl --blob-container containerName --name $eventHub --namespace-name $eventHubNamespace --resource-group $resourceGroup

# Scale the throughput units (Throughput Units)
az eventhubs namespace update --name $eventHubNamespace --sku Standard --capacity 2 --resource-group $resourceGroup

# Get the connection string for a specific event hub within a namespace
az eventhubs eventhub authorization-rule keys list --eventhub-name $eventHubName --namespace-name $eventHubNamespace --name MyAuthRuleName --resource-group $resourceGroup

# Get Event Hub details (Partitions, Consumer Groups)
az eventhubs eventhub show --name $eventHub --namespace-name $eventHubNamespace --resource-group $resourceGroup

# Delete the Event Hub Namespace (this will delete the Event Hub and Consumer Groups within it)
az eventhubs namespace delete --name $eventHubNamespace --resource-group $resourceGroup
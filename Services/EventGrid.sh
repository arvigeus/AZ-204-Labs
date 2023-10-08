# Create resource group
# Create topic
# (Optional) Create a supported service
# Use topic end service resource id
# Create subscription with topic id as source and service id as endpoint

# Create a resource group
az group create --name $resourceGroup --location $myLocation

# Create a custom topic - the endpoint where publishers send events. Used for related events
az eventgrid topic create --name $topicName --location $location --resource-group $resourceGroup

# Get resource IDs
## Topic
topicId=$(az eventgrid topic show --name $topicName --resource-group $resourceGroup --query "id" --output tsv)
## EventHub (for example)
serviceId=$(az eventhubs eventhub show --name $eventHubName --namespace-name $namespaceName --resource-group $resourceGroup --query "id" --output tsv)

# Link the Event Grid Topic to service
az eventgrid event-subscription create --name $name --resource-group $resourceGroup \
  --source-resource-id $topicId \
  --endpoint-type webhook # {eventhub,storagequeue,servicebusqueue} \
  --endpoint $serviceId # resourceId of the endpoint type; or url: https://contoso.azurewebsites.net/api/f1?code=code
  # [--expiration-date] - for temporary needs; no need to cleanup afterwards
  # Batching
  # [--max-events-per-batch]
  # [--preferred-batch-size-in-kilobytes] Events bigger than the size will be sent as their own batch

# Dead lettering (set empty to disable)
storageid=$(az storage account show --name demoStorage --resource-group gridResourceGroup --query id --output tsv)
az eventgrid event-subscription update --name $name \
    --deadletter-endpoint $storageid/blobServices/default/containers/$containername

# Filters
az eventgrid event-subscription update --name $name \
  --advanced-filter data.url StringBeginsWith https://myaccount.blob.core.windows.net # Can have multiple --advanced-filter (up to 25) \
  --subject-case-sensitive {false, true} \
  --subject-begins-with mysubject_prefix # ex: /blobServices/default/containers/<target> \
  --subject-ends-with mysubject_suffix # ex: .txt

# Alt: System topic for storage account
 az eventgrid system-topic create  --name $name --resource-group $resourceGroup \
    --source $storageid \
    --topic-type microsoft.storage.storageaccounts

# Send an event to the custom topic
## Need to pass key as aeg-sas-key header
topicEndpoint=$(az eventgrid topic show --name $topicName -g $resourceGroup --query "endpoint" --output tsv)
key=$(az eventgrid topic key list --name $topicName -g $resourceGroup --query "key1" --output tsv)
event='[ {"id": "'"$RANDOM"'", "eventType": "recordInserted", "subject": "myapp/vehicles/motorcycles", "eventTime": "'`date +%Y-%m-%dT%H:%M:%S%z`'", "data":{ "make": "Contoso", "model": "Monster"},"dataVersion": "1.0"} ]'
curl -X POST -H "aeg-sas-key: $key" -d "$event" $topicEndpoint
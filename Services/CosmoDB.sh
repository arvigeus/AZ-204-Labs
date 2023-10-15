# Create a Cosmos DB account
az cosmosdb create --name $account --kind GlobalDocumentDB ...

# Create a database
az cosmosdb sql database create --account-name $account --name $database # throughput

# Create a container
az cosmosdb sql container create --account-name $account --database-name $database --name $container --partition-key-path "/mypartitionkey"

# Create an item
az cosmosdb sql container item create --account-name $account --database-name $database --container-name $container --value "{\"id\": \"1\", \"mypartitionkey\": \"mypartitionvalue\", \"description\": \"mydescription\"}"
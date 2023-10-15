az redis create
    --name $name # must be globally unique
    --location $location # region where you want to create the cache
    --sku <sku> # pricing tier
    --vm-size <vm-size> # depends on the chosen tier
    --shard-count <shard-count> # number of shards to be used for clustering (Premium and Enterprise only). Max: 10
    --resource-group $resourceGroup
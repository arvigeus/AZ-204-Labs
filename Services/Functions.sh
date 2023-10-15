az functionapp plan create
    --name
    --resource-group
    --sku # F1(Free), D1(Shared), B1(Basic Small), B2(Basic Medium), B3(Basic Large), S1(Standard Small), P1V2(Premium V2 Small), I1 (Isolated Small), I2 (Isolated Medium), I3 (Isolated Large), K1 (Kubernetes)
    [--is-linux {false, true}]
    [--location]
    [--max-burst]
    [--min-instances]
    [--tags]
    [--zone-redundant] # Cannot be changed after plan creation. Minimum instance count is 3.
# az functionapp plan create -g $resourceGroup -n MyPlan --min-instances 1 --max-burst 10 --sku EP1

# Microsoft Defender: Basic
# Consumption: Serverless
# Dedicated: Predictable

# function.json Connection does not contain connection string

# List the existing application settings
az functionapp config appsettings list --name $name --resource-group $resourceGroup

# Add or update an application setting
az functionapp config appsettings set --settings CUSTOM_FUNCTION_APP_SETTING=12345 --name $name --resource-group $resourceGroup

# Create a new function app (Consumption)
az functionapp create --resource-group $resourceGroup --name $consumptionFunctionName --consumption-plan-location $regionName --runtime dotnet --functions-version 3 --storage-account $storageName

# Get the default (host) key that can be used to access any HTTP triggered function in the function app
subName='<SUBSCRIPTION_ID>'
resGroup=AzureFunctionsContainers-rg
appName=glengagtestdocker
path=/subscriptions/$subName/resourceGroups/$resGroup/providers/Microsoft.Web/sites/$appName/host/default/listKeys?api-version=2018-11-01
az rest --method POST --uri $path --query functionKeys.default --output tsv

az functionapp config appsettings set --settings SCALE_CONTROLLER_LOGGING_ENABLED=AppInsights:Verbose
az functionapp config appsettings delete --setting-names SCALE_CONTROLLER_LOGGING_ENABLED
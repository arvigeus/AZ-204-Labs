az apim create --name MyAPIMInstance --resource-group $resourceGroup --location eastus --publisher-name "My Publisher" --publisher-email publisher@example.com --sku-name Developer

# Add a secret (nv - named value)
az apim nv create --resource-group $resourceGroup \
    --display-name "named_value_01" --named-value-id named_value_01 \
    --secret true --service-name apim-hello-world --value test

# To use a named value in a policy, place its display name inside a double pair of braces like `{{ContosoHeader}}`.
# If the value is an expression, it will be evaluated. If the value is the name of another named value - not.


# Calling API with Subscription Key
# Ocp-Apim-Subscription-Key header with subscription-key
curl --header "Ocp-Apim-Subscription-Key: <key string>" https://<apim gateway>.azure-api.net/api/path
curl https://<apim gateway>.azure-api.net/api/path?subscription-key=<key string>


# _Header-based versioning_ if the _URL has to stay the same_. Revisions and other types of versioning schemas require modified URL.

# Creating separate gateways or web APIs would force users to access a different endpoint. A separate gateway provides complete isolation.
az apim api release create --resource-group $resourceGroup \
    --api-id demo-conference-api --api-revision 2 --service-name apim-hello-world \
    --notes 'Testing revisions. Added new "test" operation.'
az group deployment create --resource-group $resourceGroup --template-file ./apis.json --parameters apiRevision="20191206" apiVersion="v1" serviceName=<serviceName> apiVersionSetName=<versionSetName> apiName=<apiName> apiDisplayName=<displayName>
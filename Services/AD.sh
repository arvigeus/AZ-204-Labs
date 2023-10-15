# Creating a resource (like a VM or any other service that supports it) with a system-assigned identity
az <service> create --resource-group $resourceGroup --name myResource --assign-identity '[system]'

# Assigning a system-assigned identity to an existing resource
az <service> identity assign --resource-group $resourceGroup --name myResource --identities '[system]'

# First, create the identity
az identity create --resource-group $resourceGroup --name identityName

# Creating a resource (like a VM or any other service that supports it) with a user-assigned identity
az <service> create --assign-identity $identityName --resource-group $resourceGroup --name $resourceName
#az <service> create --assign-identity '/subscriptions/<SubId>/resourcegroups/$resourceGroup/providers/Microsoft.ManagedIdentity/userAssignedIdentities/myIdentity' --resource-group $resourceGroup --name $resourceName

# Assigning a user-assigned identity to an existing resource
az <service> identity assign --identities $identityName --resource-group $resourceGroup --name $resourceName
# az <service> identity assign --identities '/subscriptions/<SubId>/resourcegroups/$resourceGroup/providers/Microsoft.ManagedIdentity/userAssignedIdentities/myIdentity' --resource-group $resourceGroup --name $resourceName

# Here ROLE is about permissions what to do, SCOPE is where these permissions apply to
az role assignment create --assignee <PrincipalId> --role <RoleName> --scope <Scope>

# To protect an API in Azure API Management, register both the backend API and web app, configure permissions to allow the web app to call the backend API
az ad app permission add --id <WebApp-Application-Id> --api <Backend-API-Application-Id> --api-permissions <Scope-Permission-UUID>=Scope # delegated permissions (user)
az ad app permission add --id <WebApp-Application-Id> --api <Backend-API-Application-Id> --api-permissions <Role-Permission-UUID>=Role # application permission
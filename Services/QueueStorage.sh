az storage account create --name mystorageaccount --resource-group $resourceGroup --location eastus --sku Standard_LRS
az storage queue create --name myqueue --account-name mystorageaccount
az storage queue list --account-name mystorageaccount --output table
az storage message put --queue-name myqueue --account-name mystorageaccount --content "Hello, World!"
az storage message peek --queue-name myqueue --account-name mystorageaccount
az storage message get --queue-name myqueue --account-name mystorageaccount
az storage message delete --queue-name myqueue --account-name mystorageaccount --message-id <message-id> --pop-receipt <pop-receipt>
az storage queue delete --name myqueue --account-name mystorageaccount
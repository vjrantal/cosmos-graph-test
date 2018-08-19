Param(
  [string]$resourceGroup,
  [string]$cosmosdbAccount,
  [string]$database,
  [string]$collection,  
  [bool]$emptyCollection = $true,
  [bool]$rebuildImage = $false  
)

$ErrorActionPreference = "Stop"

# One Cosmos DB partition is 10GB or 10k RUs. Total Container Instances shouldn't exceed 20.
$ruThroughput = 100000
$instances = [math]::min($ruThroughput / 10000, 20)

$partitionKey = "partitionId"
$batchSize = 5000
$acrName = $resourceGroup.replace("-", "").replace("_", "")
$imageName = "cosmos-graph-test"
$imageTag = "1.0"

$rgExists = az group exists -n $resourceGroup -o tsv
if ($rgExists -ne "true") {
    az group create -l westeurope -n $resourceGroup
}

$existingContainers = az container list -g $resourceGroup --query "[?starts_with(name, '$imageName')].name" | ConvertFrom-Json
foreach ($container in $existingContainers) {
    az container delete -g $resourceGroup -n $container -y
}

$cosmosdbExists = az cosmosdb check-name-exists -n $cosmosdbAccount -o tsv
if ($cosmosdbExists -ne "true") {
    az cosmosdb create -g $resourceGroup -n $cosmosdbAccount --capabilities EnableGremlin
}

$databaseExists = az cosmosdb database exists -g $resourceGroup -n $cosmosdbAccount --db-name $database -o tsv
if ($databaseExists -ne "true") {
    az cosmosdb database create -g $resourceGroup -n $cosmosdbAccount --db-name $database
}

if ($emptyCollection -eq $true) {
    az cosmosdb collection delete -g $resourceGroup -n $cosmosdbAccount --db-name $database --collection-name $collection
}

$collectionExists = az cosmosdb collection exists -g $resourceGroup -n $cosmosdbAccount --db-name $database --collection-name $collection -o tsv
if ($collectionExists -ne "true") {
    az cosmosdb collection create -g $resourceGroup -n $cosmosdbAccount `
        --db-name $database --collection-name $collection `
        --throughput $ruThroughput --partition-key-path "/$partitionKey"
}

$acrExists = az acr list -g $resourceGroup --query "[].contains(name, '$acrName')" -o tsv
if ($acrExists -ne "true") {
    az acr create -g $resourceGroup -n $acrName --sku Standard --admin-enabled true
}

$acrServer = az acr show -n $acrName --query loginServer -o tsv
$acrImage = "${acrServer}/${imageName}:${imageTag}"
$imageExists = az acr repository list -n $acrName --query "[].contains(@, '$imageName')" -o tsv

if ($rebuildImage -eq $true -or $imageExists -ne "true") {
    az acr build --registry $acrName --image $acrImage --os windows .
}

$acrUsername = az acr credential show -n $acrName --query username -o tsv
$acrPassword = az acr credential show -n $acrName --query "passwords[0].value" -o tsv

$cosmosdbKey = az cosmosdb list-keys -g $resourceGroup -n $cosmosdbAccount --query primaryMasterKey -o tsv
$connectionString = "AccountEndpoint=https://$cosmosdbAccount.documents.azure.com:443/;AccountKey=$cosmosdbKey;ApiKind=Gremlin;database=$database;collection=$collection"

for ($i = 0; $i -lt $instances; $i++) {
    az container create -g $resourceGroup -n "$imageName$i" --image $acrImage `
        --restart-policy Never --os-type Windows --cpu 4 --memory 14 `
        --registry-login-server $acrServer `
        --registry-username $acrUsername --registry-password $acrPassword `
        --command-line "cosmosdb-graph-test.exe -b $batchSize -r $i -c $connectionString"
}
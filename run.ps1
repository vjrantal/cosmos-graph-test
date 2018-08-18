Param(
  [string]$resourceGroup,
  [string]$cosmosdbAccount,
  [string]$database,
  [string]$collection,  
  [bool]$emptyCollection = $false,
  [bool]$rebuildImage = $false,
  [int]$instances = 20
)

$ErrorActionPreference = "Stop"

$ruThroughput = 100000
$partitionKey = "partitionId"
$acrName = $resourceGroup.replace("-", "").replace("_", "")
$imageName = "cosmos-graph-test"
$imageTag = "1.0"

$rgExists = az group exists -n $resourceGroup -o tsv
if ($rgExists -ne "true") {
    az group create -l westeurope -n $resourceGroup
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
    az cosmosdb collection create -g $resourceGroup -n $cosmosdbAccount --db-name $database --collection-name $collection --throughput $ruThroughput --partition-key-path "/$partitionKey"
}

$acrExists = az acr list -g $resourceGroup --query "[0].name=='$acrName'" -o tsv
if ($acrExists -ne "true") {
    az acr create -g $resourceGroup -n $acrName --sku Basic --admin-enabled true
}

$acrServer = az acr show -n $acrName --query loginServer -o tsv
$acrImage = "${acrServer}/${imageName}:${imageTag}"
$imageExists = az acr repository list -n $acrName --query "[0] == '$imageName'"

if ($rebuildImage -eq $true -or $imageExists -ne "true") {
    az acr login -n $acrName
    docker build . -t $acrImage
    docker push $acrImage
}

$acrUsername = az acr credential show -n $acrName --query username
$acrPassword = az acr credential show -n $acrName --query "passwords[0].value"

$cosmosdbKey = az cosmosdb list-keys -g $resourceGroup -n $cosmosdbAccount --query primaryMasterKey -o tsv
$connectionString = "AccountEndpoint=https://$cosmosdbAccount.documents.azure.com:443/;AccountKey=$cosmosdbKey;ApiKind=Gremlin;database=$database;collection=$collection"


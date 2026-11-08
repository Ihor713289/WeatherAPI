// Storage account with everything the solution uses:
// - Blob containers: generated images (private, served via SAS) and the Unsplash background cache
// - Queues: start queue + per-station image queue (and their poison queues)
// - Tables: generation processes and per-station image status

@description('Storage account name (3-24 lowercase letters/digits, globally unique).')
@minLength(3)
@maxLength(24)
param name string

param location string
param tags object = {}

param imagesContainerName string = 'weather-images'
param backgroundCacheContainerName string = 'temp-backgrounds'
param processesQueueName string = 'generation-processes'
param imagesQueueName string = 'weather-images'
param processesTableName string = 'CreationProcesses'
param imagesTableName string = 'GeneratedImages'

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false // images are only reachable through SAS links
    allowSharedKeyAccess: true // required: the API signs SAS URLs with the account key
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource containers 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = [
  for containerName in [imagesContainerName, backgroundCacheContainerName]: {
    parent: blobService
    name: containerName
    properties: {
      publicAccess: 'None'
    }
  }
]

resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource queues 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = [
  for queueName in [
    processesQueueName
    '${processesQueueName}-poison'
    imagesQueueName
    '${imagesQueueName}-poison'
  ]: {
    parent: queueService
    name: queueName
  }
]

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource tables 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = [
  for tableName in [processesTableName, imagesTableName]: {
    parent: tableService
    name: tableName
  }
]

output name string = storage.name
output id string = storage.id

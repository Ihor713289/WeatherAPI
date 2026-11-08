// ImageGenerator as an event-driven Container Apps Job.
// Fan-out: KEDA watches the images queue and starts one execution per station message. Each execution
// renders exactly one station image, records it in Table storage and exits. The last finished image
// marks the whole process as Completed.

param name string
param location string
param tags object = {}

param environmentId string
param registryLoginServer string
param identityId string

@description('Full image reference, e.g. myregistry.azurecr.io/weatherapi-generator:<sha>.')
param image string

param storageAccountName string
param imagesQueueName string = 'weather-images'

@secure()
@description('Unsplash API access key.')
param unsplashAccessKey string

@description('Upper bound on simultaneously running executions; one per station (Buienradar has ~40-50).')
param maxExecutions int = 50

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

resource job 'Microsoft.App/jobs@2024-03-01' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    environmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      triggerType: 'Event'
      replicaTimeout: 300 // seconds; one execution renders a single image
      replicaRetryLimit: 1
      eventTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
        scale: {
          minExecutions: 0
          maxExecutions: maxExecutions
          pollingInterval: 15
          rules: [
            {
              name: 'images-queue'
              type: 'azure-queue'
              metadata: {
                accountName: storage.name
                queueName: imagesQueueName
                queueLength: '1' // one execution per queue message
              }
              auth: [
                {
                  secretRef: 'storage-connection-string'
                  triggerParameter: 'connection'
                }
              ]
            }
          ]
        }
      }
      registries: [
        {
          server: registryLoginServer
          identity: identityId
        }
      ]
      secrets: [
        {
          name: 'storage-connection-string'
          value: storageConnectionString
        }
        {
          name: 'unsplash-access-key'
          value: unsplashAccessKey
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'generator'
          image: image
          resources: {
            cpu: json('1.0')
            memory: '2Gi'
          }
          env: [
            { name: 'Storage__ConnectionString', secretRef: 'storage-connection-string' }
            { name: 'Unsplash__AccessKey', secretRef: 'unsplash-access-key' }
            { name: 'Job__MaxMessagesPerExecution', value: '1' }
            { name: 'Job__MaxConcurrency', value: '1' }
          ]
        }
      ]
    }
  }
}

output name string = job.name

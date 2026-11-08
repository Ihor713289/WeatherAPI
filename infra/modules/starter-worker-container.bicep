// ImageStarter as an event-driven Container Apps Job.
// KEDA watches the processes queue; every start message triggers an execution that fetches the
// Buienradar stations, fans out one message per station onto the images queue and exits.

param name string
param location string
param tags object = {}

param environmentId string
param registryLoginServer string
param identityId string

@description('Full image reference, e.g. myregistry.azurecr.io/weatherapi-starter:<sha>.')
param image string

param storageAccountName string
param processesQueueName string = 'generation-processes'

param maxExecutions int = 3

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
      replicaTimeout: 300 // seconds; one execution handles a single start message
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
              name: 'processes-queue'
              type: 'azure-queue'
              metadata: {
                accountName: storage.name
                queueName: processesQueueName
                queueLength: '1'
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
      ]
    }
    template: {
      containers: [
        {
          name: 'starter'
          image: image
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            { name: 'Storage__ConnectionString', secretRef: 'storage-connection-string' }
            { name: 'Job__MaxMessagesPerExecution', value: '1' }
            { name: 'Job__MaxConcurrency', value: '1' }
          ]
        }
      ]
    }
  }
}

output name string = job.name

// Public HTTP API (WeatherAPI) as a Container App.

param name string
param location string
param tags object = {}

param environmentId string
param registryLoginServer string
param identityId string

@description('Full image reference, e.g. myregistry.azurecr.io/weatherapi-api:<sha>.')
param image string

param storageAccountName string

@description('0 = scale to zero when idle (cheapest, first call after idle has a cold start).')
param minReplicas int = 0
param maxReplicas int = 3

@description('Lifetime of the SAS links returned for generated images.')
param sasTokenExpirationMinutes int = 60

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

resource api 'Microsoft.App/containerApps@2024-03-01' = {
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
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
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
          name: 'api'
          image: image
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'Storage__ConnectionString', secretRef: 'storage-connection-string' }
            { name: 'SasTokenExpirationMinutes', value: string(sasTokenExpirationMinutes) }
          ]
          probes: [
            {
              type: 'Liveness'
              tcpSocket: { port: 8080 }
              initialDelaySeconds: 5
              periodSeconds: 30
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
}

output name string = api.name
output fqdn string = api.properties.configuration.ingress.fqdn
output url string = 'https://${api.properties.configuration.ingress.fqdn}'

// WeatherAPI infrastructure (resource group scope).
//
// Deployed in two phases by .github/workflows/build.yml:
//   1. deployApps=false -> storage, registry + pull identity, Container Apps environment
//   2. images are pushed to the registry
//   3. deployApps=true  -> API Container App + ImageStarter / ImageGenerator Container Apps Jobs
// Both phases are idempotent, so every run can execute them in order.

targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Short prefix for resource names (lowercase letters/digits).')
@minLength(2)
@maxLength(8)
param namePrefix string = 'weather'

@description('Deploy the API and jobs. Requires the images with tag `imageTag` to exist in the registry.')
param deployApps bool = false

@description('Tag of the three images in the registry (the workflow uses the commit SHA).')
param imageTag string = 'latest'

@secure()
@description('Unsplash API access key (only used when deployApps = true).')
param unsplashAccessKey string = ''

@description('Minimum API replicas; 0 allows scale to zero.')
param apiMinReplicas int = 0

var suffix = uniqueString(resourceGroup().id)
var tags = {
  application: 'weatherapi'
}

// Keep in sync with the "Storage" section in the appsettings.json files.
var processesQueueName = 'generation-processes'
var imagesQueueName = 'weather-images'

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    name: take('${namePrefix}st${suffix}', 24)
    location: location
    tags: tags
    processesQueueName: processesQueueName
    imagesQueueName: imagesQueueName
  }
}

module registry 'modules/registry.bicep' = {
  name: 'registry'
  params: {
    name: take('${namePrefix}acr${suffix}', 50)
    identityName: '${namePrefix}-id-acrpull'
    location: location
    tags: tags
  }
}

module containerAppsEnvironment 'modules/container-apps-environment.bicep' = {
  name: 'container-apps-environment'
  params: {
    name: '${namePrefix}-cae'
    logAnalyticsName: '${namePrefix}-log-${suffix}'
    location: location
    tags: tags
  }
}

module api 'modules/api-container-app.bicep' = if (deployApps) {
  name: 'api'
  params: {
    name: '${namePrefix}-api'
    location: location
    tags: tags
    environmentId: containerAppsEnvironment.outputs.id
    registryLoginServer: registry.outputs.loginServer
    identityId: registry.outputs.identityId
    image: '${registry.outputs.loginServer}/weatherapi-api:${imageTag}'
    storageAccountName: storage.outputs.name
    minReplicas: apiMinReplicas
  }
}

module starterJob 'modules/starter-worker-container.bicep' = if (deployApps) {
  name: 'starter-job'
  params: {
    name: '${namePrefix}-job-starter'
    location: location
    tags: tags
    environmentId: containerAppsEnvironment.outputs.id
    registryLoginServer: registry.outputs.loginServer
    identityId: registry.outputs.identityId
    image: '${registry.outputs.loginServer}/weatherapi-starter:${imageTag}'
    storageAccountName: storage.outputs.name
    processesQueueName: processesQueueName
  }
}

module generatorJob 'modules/generator-worker-container.bicep' = if (deployApps) {
  name: 'generator-job'
  params: {
    name: '${namePrefix}-job-generator'
    location: location
    tags: tags
    environmentId: containerAppsEnvironment.outputs.id
    registryLoginServer: registry.outputs.loginServer
    identityId: registry.outputs.identityId
    image: '${registry.outputs.loginServer}/weatherapi-generator:${imageTag}'
    storageAccountName: storage.outputs.name
    imagesQueueName: imagesQueueName
    unsplashAccessKey: unsplashAccessKey
  }
}

output registryName string = registry.outputs.name
output registryLoginServer string = registry.outputs.loginServer
output storageAccountName string = storage.outputs.name
output environmentId string = containerAppsEnvironment.outputs.id
output apiUrl string = deployApps ? api!.outputs.url : ''

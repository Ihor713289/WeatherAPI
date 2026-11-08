// Log Analytics workspace + Container Apps managed environment (Consumption) shared by the API and both jobs.

param name string
param logAnalyticsName string
param location string
param tags object = {}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// The mode must be explicit: without `environmentMode`, Azure can create an *express* environment,
// and express environments reject Container Apps Jobs (ExpressEnvironmentResourceNotSupported).
// The property is only available in preview API versions, hence the version below.
// See https://learn.microsoft.com/azure/container-apps/express-faq
resource environment 'Microsoft.App/managedEnvironments@2025-10-02-preview' = {
  name: name
  location: location
  tags: tags
  properties: {
    // Older Bicep type definitions may not know environmentMode yet; the property is still sent.
    #disable-next-line BCP037
    environmentMode: 'WorkloadProfiles'
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

output id string = environment.id
output name string = environment.name

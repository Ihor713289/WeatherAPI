// One-time bootstrap, deployed manually (see infra/README.md).
//
// Creates the identity GitHub Actions logs in with. It is a *user-assigned managed identity* with a
// federated credential instead of an Entra app registration, so it can be created with Azure RBAC
// permissions on the resource group only (no "Application Developer" directory role needed).

targetScope = 'resourceGroup'

param location string = resourceGroup().location

@description('Name of the managed identity used by GitHub Actions.')
param name string = 'id-github-weatherapi'

@description('GitHub repository as "owner/repo".')
param repository string

@description('GitHub environment used by the deploy job (must match `environment:` in build.yml).')
param environmentName string = 'production'

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: name
  location: location
}

// Trust OIDC tokens issued to the workflow's deploy job.
resource githubCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: identity
  name: 'github-${environmentName}'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${repository}:environment:${environmentName}'
    audiences: [
      'api://AzureADTokenExchange'
    ]
  }
}

// Owner on this resource group only: main.bicep creates a role assignment (AcrPull for the apps'
// pull identity), which Contributor is not allowed to do.
var ownerRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8e3af657-a8ff-443c-a75c-2fe8c4bcb635')

resource owner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, identity.id, ownerRoleId)
  properties: {
    roleDefinitionId: ownerRoleId
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

output clientId string = identity.properties.clientId
output tenantId string = identity.properties.tenantId
output subscriptionId string = subscription().subscriptionId

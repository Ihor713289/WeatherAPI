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

@description('Numeric GitHub owner (user/org) id: `gh api repos/<owner>/<repo> --jq .owner.id`. Needed for the immutable subject format.')
param ownerId string = ''

@description('Numeric GitHub repository id: `gh api repos/<owner>/<repo> --jq .id`. Needed for the immutable subject format.')
param repositoryId string = ''

@description('GitHub environment used by the deploy job (must match `environment:` in build.yml).')
param environmentName string = 'production'

var ownerName = split(repository, '/')[0]
var repositoryName = split(repository, '/')[1]

// GitHub issues the token subject in one of two formats (repositories can opt into / be migrated to the
// immutable one). Trust both, so login keeps working whichever one GitHub presents:
//   repo:<owner>/<repo>:environment:<env>
//   repo:<owner>@<ownerId>/<repo>@<repoId>:environment:<env>
var subjects = concat(
  [
    { name: 'github-${environmentName}', subject: 'repo:${repository}:environment:${environmentName}' }
  ],
  empty(ownerId) || empty(repositoryId)
    ? []
    : [
        {
          name: 'github-${environmentName}-immutable'
          subject: 'repo:${ownerName}@${ownerId}/${repositoryName}@${repositoryId}:environment:${environmentName}'
        }
      ]
)

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: name
  location: location
}

// Trust OIDC tokens issued to the workflow's deploy job. Azure rejects concurrent writes of federated
// credentials on the same identity, hence batchSize(1).
@batchSize(1)
resource githubCredentials 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = [
  for credential in subjects: {
    parent: identity
    name: credential.name
    properties: {
      issuer: 'https://token.actions.githubusercontent.com'
      subject: credential.subject
      audiences: [
        'api://AzureADTokenExchange'
      ]
    }
  }
]

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

@description('The name of the AI Search service to enable managed identity on.')
param searchServiceName string

@description('The Azure region where the search service is deployed.')
param location string

@description('Tags to apply to the search service.')
param tags object = {}

// Redeploys the same search service, so tags must be repeated or they are stripped.
resource aiSearchWithManagedIdentity 'Microsoft.Search/searchServices@2024-06-01-preview' = {
  name: searchServiceName
  location: location
  tags: union(tags, { SecurityControl: 'Ignore' })
  sku: {
    name: 'basic'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
    networkRuleSet: {
      ipRules: []
    }
    encryptionWithCmk: {
      enforcement: 'Unspecified'
    }
    disableLocalAuth: true
    semanticSearch: 'free'
  }
}

@description('The principal ID of the search service system-assigned managed identity.')
output principalId string = aiSearchWithManagedIdentity.identity.principalId

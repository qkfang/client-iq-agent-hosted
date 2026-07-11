// Creates the Windows App Service Plan, the onboarding Function App and the
// CRM Web App, plus the storage container and role assignments they need to
// pick up onboarding forms and call the Foundry onboarding agent.

@description('The name of the App Service Plan.')
param appServicePlanName string

@description('The name of the Function App.')
param functionAppName string

@description('The name of the Web App.')
param webAppName string

@description('The Azure region where resources will be deployed.')
param location string

@description('The name of the storage account used for the Function App and onboarding form uploads.')
param storageAccountName string

@description('The name of the blob container that receives onboarding forms for processing.')
param onboardingContainerName string = 'onboarding-forms'

@description('The name of the Service Bus namespace used for onboarding requests.')
param serviceBusNamespaceName string

@description('The fully qualified Service Bus namespace host name.')
param serviceBusFullyQualifiedNamespace string

@description('The name of the Service Bus queue used for onboarding requests.')
param serviceBusQueueName string

@description('The Azure AI Foundry project endpoint used by the Function App to call the onboarding agent.')
param foundryProjectEndpoint string

@description('The Azure AI Foundry agent id for the onboarding agent.')
param foundryOnboardingAgentId string = ''

@description('The name of the Azure AI Services account to grant the Function App access to.')
param aiServicesName string

@description('The Application Insights connection string used for monitoring the Function App and Web App.')
param applicationInsightsConnectionString string = ''

@description('Tags to apply to the deployed resources.')
param tags object = {}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource onboardingContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${storageAccountName}/default/${onboardingContainerName}'
}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  kind: 'app'
  sku: {
    name: 'S1'
    tier: 'Standard'
  }
  properties: {
    reserved: false
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  tags: union(tags, { 'azd-service-name': 'func' })
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v10.0'
      use32BitWorkerProcess: false
      appSettings: [
        { name: 'AzureWebJobsStorage__accountName', value: storageAccountName }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'ONBOARDING_CONTAINER_NAME', value: onboardingContainerName }
        { name: 'ServiceBus__FullyQualifiedNamespace', value: serviceBusFullyQualifiedNamespace }
        { name: 'ServiceBus__QueueName', value: serviceBusQueueName }
        { name: 'Foundry__ProjectEndpoint', value: foundryProjectEndpoint }
        { name: 'Foundry__OnboardingAgentId', value: foundryOnboardingAgentId }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: applicationInsightsConnectionString }
      ]
    }
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  tags: union(tags, { 'azd-service-name': 'web' })
  kind: 'app'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v10.0'
      use32BitWorkerProcess: false
      appSettings: [
        { name: 'Foundry__ProjectEndpoint', value: foundryProjectEndpoint }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: applicationInsightsConnectionString }
      ]
    }
  }
}

// ========== Role Assignments ========== //

resource storageBlobDataContributor 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe' // Storage Blob Data Contributor
}

resource azureServiceBusDataOwner 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: '090c5cfd-751d-490a-894a-3ce6f1109419' // Azure Service Bus Data Owner
}

resource azureAIUser 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: '53ca6127-db72-4b80-b1b0-d745d6d5456d' // Azure AI User
}

resource functionAppStorageBlobDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  name: guid(storageAccount.id, functionApp.id, storageBlobDataContributor.id)
  properties: {
    principalId: functionApp.identity.principalId
    roleDefinitionId: storageBlobDataContributor.id
    principalType: 'ServicePrincipal'
  }
}

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2024-01-01' existing = {
  name: serviceBusNamespaceName
}

resource functionAppServiceBusDataOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: serviceBusNamespace
  name: guid(serviceBusNamespace.id, functionApp.id, azureServiceBusDataOwner.id)
  properties: {
    principalId: functionApp.identity.principalId
    roleDefinitionId: azureServiceBusDataOwner.id
    principalType: 'ServicePrincipal'
  }
}

resource aiServices 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: aiServicesName
}

resource functionAppAzureAIUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: aiServices
  name: guid(aiServices.id, functionApp.id, azureAIUser.id)
  properties: {
    principalId: functionApp.identity.principalId
    roleDefinitionId: azureAIUser.id
    principalType: 'ServicePrincipal'
  }
}

@description('The name of the App Service Plan.')
output appServicePlanName string = appServicePlan.name

@description('The name of the Function App.')
output functionAppName string = functionApp.name

@description('The default host name of the Function App.')
output functionAppHostName string = functionApp.properties.defaultHostName

@description('The name of the Web App.')
output webAppName string = webApp.name

@description('The default host name of the Web App.')
output webAppHostName string = webApp.properties.defaultHostName

@description('The MCP endpoint exposed by the Web App for the Foundry onboarding agent to call.')
output webAppMcpEndpoint string = 'https://${webApp.properties.defaultHostName}/mcp'

@description('The name of the onboarding forms blob container.')
output onboardingContainerName string = onboardingContainerName

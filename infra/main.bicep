// ========== Microsoft IQ Solution Accelerator ========== //
// Combined deployment for Fabric Capacity and AI Foundry resources
targetScope = 'resourceGroup'

metadata name = 'Microsoft IQ Solution Accelerator'
metadata description = 'CSA CTO Gold Standard Solution Accelerator for Unified Data Foundation with Fabric and AI Foundry.'

// ========== Common Parameters ========== //
var abbrs = loadJsonContent('./abbreviations.json')

@minLength(1)
@maxLength(20)
@description('A friendly string representing the application/solution name to give to all resource names in this deployment. This should be 3-20 characters long.')
param solutionName string = 'miqsa'

@maxLength(5)
@description('A unique text value for the solution. This is used to ensure resource names are unique for global resources. Defaults to a 5-character substring of the unique string generated from the subscription ID, resource group name, and solution name.')
param solutionUniqueText string = substring(uniqueString(subscription().id, resourceGroup().name, solutionName), 0, 5)

@minLength(3)
@metadata({ azd: { type: 'location' } })
@description('Azure region for all services. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('Optional. Enable/Disable usage telemetry for module.')
param enableTelemetry bool = true

@description('Optional. Created by user name.')
param createdBy string = contains(deployer(), 'userPrincipalName') ? split(deployer().userPrincipalName, '@')[0] : deployer().objectId

// ========== Fabric Parameters ========== //

@description('Optional. An array of user object IDs or service principal object IDs that will be assigned the Fabric Capacity Admin role. This can be used to add additional admins beyond the default admin which is the user assigned managed identity created as part of this deployment.')
param fabricAdminMembers array = []

@allowed([
  'F2'
  'F4'
  'F8'
  'F16'
  'F32'
  'F64'
  'F128'
  'F256'
  'F512'
  'F1024'
  'F2048'
])
@description('Optional. SKU tier of the Fabric resource.')
param skuName string = 'F2'

@description('Optional. Name of an existing Fabric capacity to use. If empty, a new capacity will be created.')
param existingFabricCapacityName string = ''

// ========== AI Foundry Parameters ========== //
@description('Optional. Existing Log Analytics Workspace Resource ID')
param existingLogAnalyticsWorkspaceId string = ''

@description('Optional. Use this parameter to use an existing AI project resource ID')
param azureExistingAIProjectResourceId string = ''

@minLength(1)
@description('Industry use case for deployment')
@allowed([
  'Retail-sales-analysis'
  'Insurance-improve-customer-meetings'
])
param useCase string = 'Retail-sales-analysis'

@description('Location for AI services deployment. This is the location where the Search service resource will be deployed.')
param searchServiceLocation string = resourceGroup().location

@allowed([
  'australiaeast'
  'eastus'
  'eastus2'
  'francecentral'
  'japaneast'
  'swedencentral'
  'uksouth'
  'westus'
  'westus3'
])
@metadata({
  azd: {
    type: 'location'
    usageName: [
      'OpenAI.GlobalStandard.gpt-5-mini,150'
      'OpenAI.GlobalStandard.text-embedding-3-small,80'
    ]
  }
})
@description('Location for AI Foundry deployment. This is the location where the AI Foundry resources will be deployed.')
param aiDeploymentsLocation string

@minLength(1)
@description('GPT model deployment type')
@allowed([
  'Standard'
  'GlobalStandard'
])
param deploymentType string = 'GlobalStandard'

@description('Name of the GPT model to deploy')
param gptModelName string = 'gpt-5-mini'

@description('Version of the GPT model to deploy')
param gptModelVersion string = '2025-08-07'

@minValue(10)
@description('Capacity of the GPT deployment')
param gptDeploymentCapacity int = 150

@minLength(1)
@description('Name of the Text Embedding model to deploy')
@allowed([
  'text-embedding-3-small'
])
param embeddingModel string = 'text-embedding-3-small'

@minValue(10)
@description('Capacity of the Embedding Model deployment')
param embeddingDeploymentCapacity int = 80

@description('The principal type of the deploying user. Use ServicePrincipal for CI/CD pipelines with OIDC.')
@allowed(['User', 'ServicePrincipal'])
param deployingUserPrincipalType string = 'User'

// ========== Variables ========== //
var solutionSuffix = toLower(trim(replace(
  replace(
    replace(
      replace(
        replace(
          replace('${solutionName}${solutionUniqueText}', '-', ''),
        '_', ''),
      '.', ''),
    '/', ''),
  ' ', ''),
'*', '')))

var useExistingFabricCapacity = !empty(existingFabricCapacityName)
var deployerInfo = deployer()
var deployingUserPrincipalId = deployerInfo.objectId

// ========== Resource Group Tags ========== //
resource resourceGroupTags 'Microsoft.Resources/tags@2021-04-01' = {
  name: 'default'
  properties: {
    tags: union(
      resourceGroup().tags,
      {
        TemplateName: 'Microsoft IQ Solution Accelerator'
        CreatedBy: createdBy
        DeploymentName: deployment().name
        Type: 'Non-WAF'
      }
    )
  }
}

// ========== Fabric Capacity Deployment ========== //

var fabricCapacityResourceName = useExistingFabricCapacity ? existingFabricCapacityName : 'fc${solutionSuffix}'
var fabricCapacityDefaultAdmins = deployer().?userPrincipalName == null
  ? [deployer().objectId]
  : [deployer().userPrincipalName]
var fabricTotalAdminMembers = union(fabricCapacityDefaultAdmins, fabricAdminMembers)
module newFabricCapacity 'br/public:avm/res/fabric/capacity:0.1.2' = if (!useExistingFabricCapacity) {
  name: take('avm.res.fabric.capacity.${fabricCapacityResourceName}', 64)
  params: {
    name: fabricCapacityResourceName
    location: location
    enableTelemetry: enableTelemetry
    skuName: skuName
    adminMembers: fabricTotalAdminMembers
  }
}

// ========== Managed Identity ========== //
module managedIdentityModule 'deploy_managed_identity.bicep' = {
  name: 'deploy_managed_identity'
  params: {
    miName: '${abbrs.security.managedIdentity}${solutionSuffix}'
    solutionName: solutionName
    solutionLocation: location
  }
  scope: resourceGroup(resourceGroup().name)
}

// ========== AI Foundry and Related Resources ========== //
module aifoundry 'deploy_ai_foundry.bicep' = {
  name: 'deploy_ai_foundry'
  params: {
    solutionName: solutionName
    solutionUniqueText: solutionUniqueText
    solutionLocation: aiDeploymentsLocation
    deploymentType: deploymentType
    gptModelName: gptModelName
    gptModelVersion: gptModelVersion
    gptDeploymentCapacity: gptDeploymentCapacity
    embeddingModel: embeddingModel
    embeddingDeploymentCapacity: embeddingDeploymentCapacity
    managedIdentityObjectId: managedIdentityModule.outputs.managedIdentityOutput.objectId
    existingLogAnalyticsWorkspaceId: existingLogAnalyticsWorkspaceId
    azureExistingAIProjectResourceId: azureExistingAIProjectResourceId
    deployingUserPrincipalId: deployingUserPrincipalId
    deployingUserPrincipalType: deployingUserPrincipalType
    searchServiceLocation: searchServiceLocation
  }
  scope: resourceGroup(resourceGroup().name)
}

// ========== Container Registry (hosted agent image) ========== //
var containerRegistryResourceName = '${abbrs.containers.containerRegistry}${solutionSuffix}'
module containerRegistry 'br/public:avm/res/container-registry/registry:0.12.1' = {
  name: take('avm.res.container-registry.registry.${containerRegistryResourceName}', 64)
  params: {
    name: containerRegistryResourceName
    location: location
    enableTelemetry: enableTelemetry
    acrSku: 'Standard'
    publicNetworkAccess: 'Enabled'
    networkRuleSetDefaultAction: 'Allow'
    roleAssignments: [
      {
        roleDefinitionIdOrName: 'AcrPull'
        principalId: managedIdentityModule.outputs.managedIdentityOutput.objectId
        principalType: 'ServicePrincipal'
      }
      {
        roleDefinitionIdOrName: 'AcrPull'
        principalId: aifoundry.outputs.aiProjectPrincipalId
        principalType: 'ServicePrincipal'
      }
    ]
  }
  scope: resourceGroup(resourceGroup().name)
}

// ========== Service Bus ========== //
module serviceBus 'deploy_service_bus.bicep' = {
  name: 'deploy_service_bus'
  params: {
    serviceBusNamespaceName: '${abbrs.integration.serviceBusNamespace}${solutionSuffix}'
    serviceBusQueueName: '${abbrs.integration.serviceBusQueue}onboarding-requests'
    location: location
  }
  scope: resourceGroup(resourceGroup().name)
}

// ========== Onboarding Function App and CRM Web App ========== //
module appServices 'deploy_app_service.bicep' = {
  name: 'deploy_app_service'
  params: {
    appServicePlanName: '${abbrs.compute.appServicePlan}${solutionSuffix}'
    functionAppName: '${abbrs.compute.functionApp}${solutionSuffix}'
    webAppName: '${abbrs.compute.webApp}${solutionSuffix}'
    location: location
    storageAccountName: aifoundry.outputs.storageAccountName
    serviceBusNamespaceName: serviceBus.outputs.serviceBusNamespaceName
    serviceBusFullyQualifiedNamespace: serviceBus.outputs.serviceBusFullyQualifiedNamespace
    serviceBusQueueName: serviceBus.outputs.serviceBusQueueName
    foundryProjectEndpoint: aifoundry.outputs.projectEndpoint
    aiServicesName: aifoundry.outputs.aiServicesName
  }
  scope: resourceGroup(resourceGroup().name)
}

// ========== Outputs ========== //

// Common Outputs
@description('The location the resources were deployed to')
output AZURE_LOCATION string = location

@description('The name of the resource group')
output AZURE_RESOURCE_GROUP string = resourceGroup().name

@description('The solution name used as base for resource naming')
output SOLUTION_NAME string = solutionName

@description('The unique solution suffix of the deployed resources')
output SOLUTION_SUFFIX string = solutionSuffix

@description('The unique text appended to solution name for global uniqueness')
output SOLUTION_UNIQUE_TEXT string = solutionUniqueText

// Fabric Outputs
@description('The name of the Fabric capacity resource')
output AZURE_FABRIC_CAPACITY_NAME string = fabricCapacityResourceName

@description('The identities assigned as Fabric Capacity Admin members when a new capacity is created. When an existing capacity is used, this value is not applied and is provided for informational purposes only.')
output AZURE_FABRIC_CAPACITY_ADMINISTRATORS array = fabricTotalAdminMembers

// AI Foundry Outputs

@description('GPT model deployment name (e.g., gpt-5-mini)')
output AZURE_OPENAI_DEPLOYMENT_MODEL string = gptModelName

@description('Azure OpenAI service endpoint URL')
output AZURE_OPENAI_ENDPOINT string = aifoundry.outputs.aiServicesTarget

@description('Embedding model deployment name for vector search')
output AZURE_OPENAI_EMBEDDING_MODEL string = embeddingModel

@description('Azure AI Agent service endpoint URL')
output AZURE_AI_AGENT_ENDPOINT string = aifoundry.outputs.projectEndpoint

@description('Model deployment name used by Azure AI Agent')
output AZURE_AI_AGENT_MODEL_DEPLOYMENT_NAME string = gptModelName

@description('Deployed use case identifier (e.g., Retail-sales-analysis)')
output USE_CASE string = useCase

@description('Azure AI Search service endpoint URL')
output AZURE_AI_SEARCH_ENDPOINT string = aifoundry.outputs.aiSearchTarget

@description('Azure AI Search index name for document search')
output AZURE_AI_SEARCH_INDEX string = '${solutionSuffix}-documents'

@description('Azure AI Search service resource name')
output AZURE_AI_SEARCH_NAME string = aifoundry.outputs.aiSearchName

@description('Local path to documents folder for search indexing')
output SEARCH_DATA_FOLDER string = 'data/documents'

@description('AI Foundry connection name for Azure AI Search')
output AZURE_AI_SEARCH_CONNECTION_NAME string = aifoundry.outputs.aiSearchConnectionName

@description('Azure Storage blob service endpoint URL')
output AZURE_STORAGE_BLOB_ENDPOINT string = aifoundry.outputs.storageBlobEndpoint

@description('Azure Storage account name')
output AZURE_STORAGE_ACCOUNT_NAME string = aifoundry.outputs.storageAccountName

@description('AI Foundry connection ID for Azure AI Search')
output AZURE_AI_SEARCH_CONNECTION_ID string = aifoundry.outputs.aiSearchConnectionId

@description('Azure AI Foundry project endpoint URL')
output AZURE_AI_PROJECT_ENDPOINT string = aifoundry.outputs.projectEndpoint

@description('Azure AI Foundry resource ID for role assignments')
output AI_FOUNDRY_RESOURCE_ID string = aifoundry.outputs.aiFoundryResourceId

@description('Azure AI Foundry project name')
output AZURE_AI_PROJECT_NAME string = aifoundry.outputs.aiProjectName

@description('Azure AI Services resource name')
output AI_SERVICE_NAME string = aifoundry.outputs.aiServicesName

@description('Azure Container Registry name used to build and host the agent container image')
output AZURE_CONTAINER_REGISTRY_NAME string = containerRegistryResourceName

// Service Bus Outputs

@description('Service Bus namespace fully qualified host name')
output AZURE_SERVICE_BUS_NAMESPACE string = serviceBus.outputs.serviceBusFullyQualifiedNamespace

@description('Service Bus queue used for onboarding requests')
output AZURE_SERVICE_BUS_QUEUE_NAME string = serviceBus.outputs.serviceBusQueueName

// Onboarding Function App and CRM Web App Outputs

@description('Name of the onboarding Function App')
output AZURE_FUNCTION_APP_NAME string = appServices.outputs.functionAppName

@description('Blob container watched by the Function App for incoming onboarding forms')
output AZURE_ONBOARDING_CONTAINER_NAME string = appServices.outputs.onboardingContainerName

@description('Name of the CRM Web App')
output AZURE_WEB_APP_NAME string = appServices.outputs.webAppName

@description('MCP endpoint exposed by the CRM Web App for the Foundry onboarding agent')
output AZURE_WEB_APP_MCP_ENDPOINT string = appServices.outputs.webAppMcpEndpoint

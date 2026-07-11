// Creates the Service Bus namespace and queue used to hand off onboarding
// form processing from the Function App to downstream consumers.

@description('The name of the Service Bus namespace.')
param serviceBusNamespaceName string

@description('The name of the Service Bus queue used for onboarding requests.')
param serviceBusQueueName string

@description('The Azure region where the Service Bus namespace will be deployed.')
param location string

@description('Tags to apply to the Service Bus namespace.')
param tags object = {}

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: serviceBusNamespaceName
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
  }
}

resource serviceBusQueue 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' = {
  parent: serviceBusNamespace
  name: serviceBusQueueName
  properties: {
    maxDeliveryCount: 10
    lockDuration: 'PT1M'
    defaultMessageTimeToLive: 'P1D'
  }
}

@description('The resource ID of the Service Bus namespace.')
output serviceBusNamespaceId string = serviceBusNamespace.id

@description('The name of the Service Bus namespace.')
output serviceBusNamespaceName string = serviceBusNamespace.name

@description('The fully qualified namespace host name used for AAD-based connections.')
output serviceBusFullyQualifiedNamespace string = '${serviceBusNamespace.name}.servicebus.windows.net'

@description('The name of the onboarding requests queue.')
output serviceBusQueueName string = serviceBusQueue.name

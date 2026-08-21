// Azure Function App: func-watch-wishlist-sale
// Deploy target: existing resource group (this template does not create the resource group itself)
@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Discord webhook URL used to post sale notifications')
@secure()
param discordWebhookUrl string

@description('IsThereAnyDeal API key')
@secure()
param itadApiKey string

@description('Steam profile ID whose wishlist is watched')
param steamProfileId string

var logAnalyticsName = 'log-watch-wishlist-sale'
var appInsightsName = 'appi-watch-wishlist-sale'
var storageAccountName = 'stfuncwatchwishlistsale'
var appServicePlanName = 'asp-watch-wishlist-sale'
var functionAppName = 'func-watch-wishlist-sale'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    IngestionMode: 'LogAnalytics'
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  tags: {
    'hidden-link: /app-insights-resource-id': appInsights.id
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      ftpsState: 'FtpsOnly'
      minTlsVersion: '1.2'
      http20Enabled: false
      use32BitWorkerProcess: false
      netFrameworkVersion: 'v10.0'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED'
          value: '1'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'WEBSITE_ENABLE_SYNC_UPDATE_SITE'
          value: 'true'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'DISCORD_WEBHOOK_URL'
          value: discordWebhookUrl
        }
        {
          name: 'ITAD_API_KEY'
          value: itadApiKey
        }
        {
          name: 'STEAM_PROFILE_ID'
          value: steamProfileId
        }
      ]
    }
  }
}
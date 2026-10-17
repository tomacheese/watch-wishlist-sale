// Deploy independently from main.bicep. Only manages low-cost metric alert rules.
// The obsolete scheduled-query alert is disabled after verification.
targetScope = 'resourceGroup'

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: 'appi-watch-wishlist-sale'
}

resource notifications 'Microsoft.Insights/actionGroups@2023-01-01' existing = {
  name: 'ag-watch-wishlist-sale'
}

// Any failed execution request is unusual in the measured seven-day baseline.
resource failedExecutions 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'metric-wws-failed-executions'
  location: 'global'
  properties: {
    description: 'Wishlist sale watcher: >=1 failed Application Insights request in 15m. Check failed Functions and orchestration.'
    severity: 2
    enabled: true
    scopes: [appInsights.id]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    autoMitigate: true
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [{
        name: 'FailedRequests'
        criterionType: 'StaticThresholdCriterion'
        metricName: 'requests/failed'
        metricNamespace: 'Microsoft.Insights/components'
        operator: 'GreaterThanOrEqual'
        timeAggregation: 'Count'
        threshold: 1
      }]
    }
    actions: [{ actionGroupId: notifications.id }]
  }
}

// Repeated exceptions may reflect a real platform/app issue; a single host
// gRPC SocketException per incident is filtered by the threshold rather than KQL.
resource exceptionBurst 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'metric-wws-exception-burst'
  location: 'global'
  properties: {
    description: 'Wishlist sale watcher: >=3 Application Insights exceptions in 15m. Check application and Functions runtime health.'
    severity: 2
    enabled: true
    scopes: [appInsights.id]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    autoMitigate: true
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [{
        name: 'ExceptionBurst'
        criterionType: 'StaticThresholdCriterion'
        metricName: 'exceptions/count'
        metricNamespace: 'Microsoft.Insights/components'
        operator: 'GreaterThanOrEqual'
        timeAggregation: 'Count'
        threshold: 3
      }]
    }
    actions: [{ actionGroupId: notifications.id }]
  }
}

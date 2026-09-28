// Azure Monitor alerts. For now only database storage: the server has a fixed size (auto-grow off), so a full disk would
// stop writes. Metric alerts and action groups are global resources.

@description('Name prefix for the alert resources.')
param namePrefix string

@description('Resource id of the PostgreSQL Flexible Server.')
param postgresServerId string

@description('Email address that receives alerts; empty creates the alert without a notification.')
param alertEmail string

@description('Database storage use, in percent, that fires the alert.')
@minValue(1)
@maxValue(99)
param storagePercentThreshold int = 80

@description('Resource tags.')
param tags object

var notify = !empty(alertEmail)

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (notify) {
  name: '${namePrefix}-alerts'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'govern'
    enabled: true
    emailReceivers: [
      {
        name: 'owner'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

resource storageAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${namePrefix}-pg-storage'
  location: 'global'
  tags: tags
  properties: {
    description: 'PostgreSQL storage is above ${storagePercentThreshold}% of the fixed size; writes stop when it is full.'
    severity: 2
    enabled: true
    scopes: [
      postgresServerId
    ]
    evaluationFrequency: 'PT15M'
    windowSize: 'PT30M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'StoragePercent'
          metricNamespace: 'Microsoft.DBforPostgreSQL/flexibleServers'
          metricName: 'storage_percent'
          operator: 'GreaterThan'
          threshold: storagePercentThreshold
          timeAggregation: 'Average'
        }
      ]
    }
    actions: notify ? [
      {
        actionGroupId: actionGroup.id
      }
    ] : []
  }
}

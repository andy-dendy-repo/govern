// PostgreSQL Flexible Server on the cheapest tier (Burstable B1ms). TLS is enforced by the server default
// (require_secure_transport = on); access is limited to Azure services.

@description('Azure region.')
param location string

@description('Globally unique server name.')
param serverName string

@description('Database created for the Api.')
param databaseName string

@description('Administrator login (used by the Api until a least-privilege role is provisioned; see docs/operations/deployment.md).')
param administratorLogin string

@secure()
@description('Administrator password.')
param administratorPassword string

@description('Resource tags.')
param tags object

resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: serverName
  location: location
  tags: tags
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '17'
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorPassword
    storage: {
      storageSizeGB: 32
      autoGrow: 'Disabled'
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: server
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// 0.0.0.0 - 0.0.0.0 is Azure's "allow access from Azure services" rule (App Service outbound IPs).
resource allowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: server
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

@description('Server host name.')
output fullyQualifiedDomainName string = server.properties.fullyQualifiedDomainName

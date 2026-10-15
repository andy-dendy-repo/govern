// Key Vault (RBAC mode) holding the Api secrets. The Web App reads them through Key Vault references
// using its system-assigned identity (see keyVaultAccess.bicep).

@description('Azure region.')
param location string

@description('Globally unique vault name (3-24 characters).')
@maxLength(24)
param vaultName string

@secure()
@description('Npgsql connection string for the Api.')
param connectionString string

@secure()
@description('JWT HMAC signing key (at least 32 characters).')
param jwtSigningKey string

@secure()
@description('Redis connection string; used only when useRedis is true.')
param redisConnectionString string = ''

@description('Whether to store the Redis connection string (the Api runs on more than one instance).')
param useRedis bool = false

@description('Resource tags.')
param tags object

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: vaultName
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    // Irreversible: nobody (not even an Owner) can purge the JWT key or connection string before the retention ends,
    // and a deleted vault's name stays reserved for 7 days.
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
  }
}

resource connectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'ConnectionStrings--Govern'
  properties: {
    value: connectionString
  }
}

resource jwtSigningKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'Jwt--SigningKey'
  properties: {
    value: jwtSigningKey
  }
}

resource redisConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (useRedis) {
  parent: vault
  name: 'ConnectionStrings--Redis'
  properties: {
    value: redisConnectionString
  }
}

@description('Vault name.')
output name string = vault.name

@description('Versionless URI of the connection string secret.')
output connectionStringSecretUri string = connectionStringSecret.properties.secretUri

@description('Versionless URI of the JWT signing key secret.')
output jwtSigningKeySecretUri string = jwtSigningKeySecret.properties.secretUri

@description('Versionless URI of the Redis connection string secret; empty when Redis is not configured.')
output redisConnectionStringSecretUri string = useRedis ? redisConnectionStringSecret!.properties.secretUri : ''

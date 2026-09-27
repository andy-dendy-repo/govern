// Linux App Service plan (B1) + Web App for Containers running the Api image. Secrets come from Key Vault
// references resolved with the system-assigned identity.

@description('Azure region.')
param location string

@description('App Service plan name.')
param planName string

@description('Globally unique Web App name.')
param webAppName string

@description('Full image reference, e.g. ghcr.io/owner/govern-api:1.2.3.')
param containerImage string

@description('Container registry URL.')
param registryUrl string = 'https://ghcr.io'

@description('Registry user for private images; empty for public images.')
param registryUsername string = ''

@secure()
@description('Registry password or token for private images; empty for public images.')
param registryPassword string = ''

@description('Key Vault secret URI of the connection string.')
param connectionStringSecretUri string

@description('Key Vault secret URI of the JWT signing key.')
param jwtSigningKeySecretUri string

@description('Browser origins allowed by the Api CORS policy.')
param corsAllowedOrigins array

@description('Resource tags.')
param tags object

var registrySettings = empty(registryUsername) ? [] : [
  { name: 'DOCKER_REGISTRY_SERVER_USERNAME', value: registryUsername }
  { name: 'DOCKER_REGISTRY_SERVER_PASSWORD', value: registryPassword }
]

var corsSettings = [for (origin, i) in corsAllowedOrigins: { name: 'Cors__AllowedOrigins__${i}', value: origin }]

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: planName
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: webAppName
  location: location
  tags: tags
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    keyVaultReferenceIdentity: 'SystemAssigned'
    siteConfig: {
      linuxFxVersion: 'DOCKER|${containerImage}'
      alwaysOn: true
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      healthCheckPath: '/health'
      appSettings: concat([
        { name: 'WEBSITES_PORT', value: '8080' }
        { name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE', value: 'false' }
        { name: 'DOCKER_REGISTRY_SERVER_URL', value: registryUrl }
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        // App Service terminates TLS; trust X-Forwarded-* so HTTPS redirection and rate limiting see the real scheme/IP.
        { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' }
        { name: 'ConnectionStrings__Govern', value: '@Microsoft.KeyVault(SecretUri=${connectionStringSecretUri})' }
        { name: 'Jwt__SigningKey', value: '@Microsoft.KeyVault(SecretUri=${jwtSigningKeySecretUri})' }
      ], corsSettings, registrySettings)
    }
  }
}

// Basic-auth publishing credentials (publish profiles) would allow deployments that bypass OIDC.
resource scmBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: webApp
  name: 'scm'
  properties: {
    allow: false
  }
}

resource ftpBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: webApp
  name: 'ftp'
  properties: {
    allow: false
  }
}

@description('Web App name.')
output name string = webApp.name

@description('Default https host name of the Api.')
output defaultHostname string = webApp.properties.defaultHostName

@description('Object id of the Web App identity.')
output principalId string = webApp.identity.principalId

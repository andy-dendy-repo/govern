// Govern infrastructure (resource group scope):
// PostgreSQL Flexible Server (B1ms) + Key Vault + Linux App Service (B1, container) + Static Web App (Free) + alerts.
// Deployed by .github/workflows/release.yml; see docs/operations/deployment.md.
targetScope = 'resourceGroup'

@description('Azure region for the Api, database and vault.')
param location string = resourceGroup().location

@description('Static Web Apps region (only a few regions are supported).')
@allowed(['westeurope', 'eastus2', 'centralus', 'westus2', 'eastasia'])
param staticWebAppLocation string = 'westeurope'

@description('Short application name used in resource names.')
@maxLength(10)
param appName string = 'govern'

@description('Environment name used in resource names and tags.')
@allowed(['prod', 'stage'])
param environmentName string = 'prod'

@description('Api image reference, e.g. ghcr.io/owner/govern-api:1.2.3.')
param containerImage string

@description('Registry user for a private image; empty when the GHCR package is public.')
param registryUsername string = ''

@secure()
@description('Registry token for a private image.')
param registryPassword string = ''

@description('PostgreSQL administrator login.')
param postgresAdministratorLogin string = 'govern_admin'

@secure()
@description('PostgreSQL administrator password.')
param postgresAdministratorPassword string

@secure()
@description('JWT signing key (at least 32 characters).')
param jwtSigningKey string

@secure()
@description('Redis connection string (StackExchange.Redis format) for running several Api instances; empty runs one instance without Redis.')
param redisConnectionString string = ''

@description('Extra browser origins for the Api CORS policy (custom client domains). The Static Web App default host is always added.')
param extraCorsOrigins array = []

@description('Custom domain host names of the Api (e.g. api.example.com). The default azurewebsites.net host is always allowed.')
param apiCustomHostnames array = []

@description('Email address for operational alerts (database storage); empty deploys the alerts without notifications.')
param alertEmail string = ''

var suffix = uniqueString(resourceGroup().id)
var tags = {
  application: appName
  environment: environmentName
}
var databaseName = 'govern'

module postgres 'modules/postgres.bicep' = {
  name: 'postgres'
  params: {
    location: location
    serverName: '${appName}-${environmentName}-pg-${suffix}'
    databaseName: databaseName
    administratorLogin: postgresAdministratorLogin
    administratorPassword: postgresAdministratorPassword
    tags: tags
  }
}

module alerts 'modules/alerts.bicep' = {
  name: 'alerts'
  params: {
    namePrefix: '${appName}-${environmentName}'
    postgresServerId: postgres.outputs.id
    alertEmail: alertEmail
    tags: tags
  }
}

module keyVault 'modules/keyVault.bicep' = {
  name: 'keyVault'
  params: {
    location: location
    vaultName: take('${appName}-${environmentName}-${suffix}', 24)
    connectionString: 'Host=${postgres.outputs.fullyQualifiedDomainName};Database=${databaseName};Username=${postgresAdministratorLogin};Password=${postgresAdministratorPassword};Ssl Mode=VerifyFull'
    jwtSigningKey: jwtSigningKey
    redisConnectionString: redisConnectionString
    useRedis: !empty(redisConnectionString)
    tags: tags
  }
}

module staticWebApp 'modules/staticWebApp.bicep' = {
  name: 'staticWebApp'
  params: {
    location: staticWebAppLocation
    name: '${appName}-${environmentName}-client'
    tags: tags
  }
}

module appService 'modules/appService.bicep' = {
  name: 'appService'
  params: {
    location: location
    planName: '${appName}-${environmentName}-plan'
    webAppName: '${appName}-${environmentName}-api-${suffix}'
    containerImage: containerImage
    registryUsername: registryUsername
    registryPassword: registryPassword
    connectionStringSecretUri: keyVault.outputs.connectionStringSecretUri
    jwtSigningKeySecretUri: keyVault.outputs.jwtSigningKeySecretUri
    redisConnectionStringSecretUri: keyVault.outputs.redisConnectionStringSecretUri
    corsAllowedOrigins: concat(['https://${staticWebApp.outputs.defaultHostname}'], extraCorsOrigins)
    customHostnames: apiCustomHostnames
    tags: tags
  }
}

module keyVaultAccess 'modules/keyVaultAccess.bicep' = {
  name: 'keyVaultAccess'
  params: {
    vaultName: keyVault.outputs.name
    principalId: appService.outputs.principalId
  }
}

@description('Api Web App name.')
output webAppName string = appService.outputs.name

@description('Api default host name (point the Cloudflare api CNAME here).')
output apiHostname string = appService.outputs.defaultHostname

@description('Static Web App name.')
output staticWebAppName string = staticWebApp.outputs.name

@description('Client default host name (point the Cloudflare app CNAME here).')
output clientHostname string = staticWebApp.outputs.defaultHostname

// Parameters for the production deployment. Secrets are read from environment variables set by the release
// workflow from GitHub environment secrets; nothing secret is stored in this file.
using 'main.bicep'

param environmentName = 'prod'
param staticWebAppLocation = 'westeurope'
param containerImage = readEnvironmentVariable('CONTAINER_IMAGE')
param postgresAdministratorPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD')
param jwtSigningKey = readEnvironmentVariable('JWT_SIGNING_KEY')
param registryUsername = readEnvironmentVariable('REGISTRY_USERNAME', '')
param registryPassword = readEnvironmentVariable('REGISTRY_PASSWORD', '')

// Optional: set the REDIS_CONNECTION_STRING environment secret only when the Api runs on more than one instance.
param redisConnectionString = readEnvironmentVariable('REDIS_CONNECTION_STRING', '')
param extraCorsOrigins = []

// Operational alerts go to the ALERT_EMAIL repository variable; empty deploys the alerts without notifications.
param alertEmail = readEnvironmentVariable('ALERT_EMAIL', '')

// The Api's custom domain comes from the API_ORIGIN repository variable (also used for the client's Api:BaseUrl and
// CSP), so the client and the Api host allow-list can't drift apart. Empty until a custom domain is bound.
var apiOrigin = readEnvironmentVariable('API_ORIGIN', '')
param apiCustomHostnames = empty(apiOrigin) ? [] : [split(replace(apiOrigin, 'https://', ''), '/')[0]]

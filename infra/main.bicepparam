// Parameters for the production deployment. Secrets are read from environment variables set by the release
// workflow from GitHub environment secrets; nothing secret is stored in this file.
using 'main.bicep'

param environmentName = 'prod'
param staticWebAppLocation = 'westeurope'
param containerImage = readEnvironmentVariable('CONTAINER_IMAGE')
param postgresAdministratorPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD')
param jwtSigningKey = readEnvironmentVariable('JWT_SIGNING_KEY')
param extraCorsOrigins = []

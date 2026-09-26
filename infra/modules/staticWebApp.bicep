// Static Web App (Free) hosting the Blazor WASM client. Deployed by the release workflow with its deployment token.

@description('Static Web Apps region (limited set: westeurope, eastus2, centralus, westus2, eastasia).')
param location string

@description('Static Web App name.')
param name string

@description('Resource tags.')
param tags object

resource site 'Microsoft.Web/staticSites@2024-04-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    allowConfigFileUpdates: true
    stagingEnvironmentPolicy: 'Disabled'
  }
}

@description('Default https host name of the client.')
output defaultHostname string = site.properties.defaultHostname

@description('Static Web App name (used to fetch the deployment token).')
output name string = site.name

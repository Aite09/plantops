// Server-level firewall rules for the API's outbound addresses. This is a module because Bicep can only loop over
// values known when a deployment starts, and these addresses exist only once the web app does. A module's parameters
// are evaluated when the module runs, which is after the app has been created.
param sqlServerName string
param ipAddresses string[]

resource sqlServer 'Microsoft.Sql/servers@2025-01-01' existing = {
  name: sqlServerName
}

// One at a time: concurrent writes to the same server's firewall can conflict.
@batchSize(1)
resource rules 'Microsoft.Sql/servers/firewallRules@2025-01-01' = [
  for ip in ipAddresses: {
    parent: sqlServer
    name: 'app-outbound-${replace(ip, '.', '-')}'
    properties: {
      startIpAddress: ip
      endIpAddress: ip
    }
  }
]

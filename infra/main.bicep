// PlantOps production infrastructure on Azure for Students, at $0. Deployed by hand by the owner (infra/README.md):
//   PLANTOPS_JWT_SIGNING_KEY="$(openssl rand -base64 48)" \
//     az deployment group create --resource-group rg-plantops --name plantops-main --parameters infra/main.bicepparam
// CI never runs this file. It holds the only definition of the app settings (each deployment replaces all of them),
// and deploying it needs rights the pipeline must not have.
targetScope = 'resourceGroup'

@description('Region for the API, database and identities. Must be allowed by the subscription, offer F1 Linux at $0 and offer serverless SQL (README step 4). The free database region can never change for this subscription.')
param location string

@description('Region for the Static Web App. SWA Free exists in only a few regions; it stores metadata there and serves files from a global edge.')
param staticWebAppLocation string

@description('Exact OIDC subject GitHub sends for the deploy job (immutable format: owner and repository IDs are part of it).')
param githubOidcSubject string

@description('User principal name of the Entra user who administers the SQL server (the subscription owner).')
param sqlAdminLogin string

@description('Object ID of that Entra user.')
param sqlAdminObjectId string

@secure()
@minLength(32)
@description('HMAC key that signs JWTs. Generated fresh for every deployment and never stored elsewhere, so each run of this file also rotates it (users simply sign in again).')
param jwtSigningKey string

@description('Keep the JWT key in Key Vault (referenced from the app setting) instead of the app setting itself. Off by default: Key Vault operations cost a fraction of a cent, which is not provably $0 on this offer. Turning it on first needs guardrails.bicepparam allowKeyVault = true, redeployed (README step 5); otherwise policy refuses the vault.')
param enableKeyVault bool = false

// Stable, unique names: the same resource group always produces the same suffix, so a redeploy updates in place.
var suffix = uniqueString(resourceGroup().id)
var names = {
  plan: 'asp-plantops-${suffix}'
  api: 'app-plantops-${suffix}'
  staticWebApp: 'stapp-plantops-${suffix}'
  sqlServer: 'sql-plantops-${suffix}'
  database: 'sqldb-plantops'
  appIdentity: 'id-plantops-app-${suffix}'
  deployIdentity: 'id-plantops-deploy-${suffix}'
  keyVault: 'kv-plantops-${take(suffix, 11)}' // vault names are limited to 24 characters
}
var jwtSecretName = 'jwt-signing-key'

// Built-in role definition IDs (https://learn.microsoft.com/azure/role-based-access-control/built-in-roles).
var websiteContributorRoleId = 'de139f84-1756-47ae-9be6-808fbbe84772'
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

// ---------------------------------------------------------------------------------------------------------------
// Identities. Both are user-assigned, so their client IDs are template outputs: the SQL users are created from
// those IDs (infra/sql/grant-identities.sql), and the identities exist before the web app first starts.
// ---------------------------------------------------------------------------------------------------------------

resource appIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: names.appIdentity
  location: location
}

resource deployIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: names.deployIdentity
  location: location
}

// No secret anywhere: GitHub's OIDC token is exchanged for an Entra token of the deploy identity. Matching is exact
// and case-sensitive, with no wildcards, so only jobs in this repository's 'production' environment qualify, and that
// environment admits only the main branch.
resource deployGitHubCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2024-11-30' = {
  parent: deployIdentity
  name: 'github-production'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: githubOidcSubject
    audiences: ['api://AzureADTokenExchange']
  }
}

// ---------------------------------------------------------------------------------------------------------------
// API: App Service F1 on Linux
// ---------------------------------------------------------------------------------------------------------------

resource plan 'Microsoft.Web/serverfarms@2025-03-01' = {
  name: names.plan
  location: location
  kind: 'linux'
  // F1 is $0. Going over its quotas (60 CPU minutes a day, 165 MB outbound a day, 1 GB storage) stops the app until
  // the quota resets; it never bills. Every other SKU bills, and guardrails.bicep denies them.
  sku: { name: 'F1', tier: 'Free' }
  properties: {
    reserved: true // Linux
  }
}

resource site 'Microsoft.Web/sites@2025-03-01' = {
  name: names.api
  location: location
  kind: 'app,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${appIdentity.id}': {} }
  }
  properties: {
    serverFarmId: plan.id
    // Secure unique default hostname: <name>-<hash>.<region>-NN.azurewebsites.net. Nobody outside this tenant can
    // ever claim it, so a stale DNS record or bookmark can't be taken over. Can only be set at creation, and Bicep must
    // opt in (the portal does this by default). If creation fails because F1 doesn't support it, delete this line
    // before the first successful deployment (README step 6).
    autoGeneratedDomainNameLabelScope: 'TenantReuse'
    httpsOnly: true              // the platform redirects HTTP to HTTPS; the API itself never redirects
    clientAffinityEnabled: false // a stateless API needs no ARR affinity cookie
    publicNetworkAccess: 'Enabled'
    keyVaultReferenceIdentity: appIdentity.id // used only for Key Vault references (enableKeyVault)
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0' // built-in .NET 10 image: plain code deploy, no container registry
      alwaysOn: false                   // not available on F1; the app unloads after 20 idle minutes
      // Liveness only: never touches SQL, so the probe that runs every minute can't keep the serverless database awake.
      healthCheckPath: '/health/live'
      http20Enabled: true
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      ftpsState: 'Disabled'
    }
  }
}

// Deployments use Entra (OIDC). Username and password publishing is switched off for FTP and for Kudu (SCM).
resource ftpBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2025-03-01' = {
  parent: site
  name: 'ftp'
  properties: { allow: false }
}

resource scmBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2025-03-01' = {
  parent: site
  name: 'scm'
  properties: { allow: false }
}

// The complete set of app settings. A deployment REPLACES the whole collection, so this resource is their only owner:
// nothing else (CI, CLI, portal) sets app settings.
resource appSettings 'Microsoft.Web/sites/config@2025-03-01' = {
  parent: site
  name: 'appsettings'
  properties: {
    ASPNETCORE_ENVIRONMENT: 'Production'
    // No password: the API signs in to SQL as its user-assigned identity (User Id = that identity's client ID).
    ConnectionStrings__PlantOps: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${database.name};Authentication=Active Directory Managed Identity;User Id=${appIdentity.properties.clientId};Encrypt=True;TrustServerCertificate=False;Connect Timeout=30'
    // App settings are encrypted at rest. With Key Vault on, this holds only a reference the platform resolves.
    Jwt__SigningKey: enableKeyVault ? '@Microsoft.KeyVault(VaultName=${names.keyVault};SecretName=${jwtSecretName})' : jwtSigningKey
    // The one browser origin allowed to call the API: the exact SWA hostname, no wildcard, no trailing slash.
    Cors__AllowedOrigins__0: 'https://${staticWebApp.properties.defaultHostname}'
    // TLS ends at App Service's front end. This makes ASP.NET Core read X-Forwarded-For/-Proto, so the per-IP login
    // rate limit sees each visitor instead of the front end. The current image sets it too, but undocumented. That a
    // client can't spoof the value is checked after the first deploy (README step 11.4), with a one-line fallback.
    ASPNETCORE_FORWARDEDHEADERS_ENABLED: 'true'
    // The pipeline uploads a finished build; App Service must not try to build it again.
    SCM_DO_BUILD_DURING_DEPLOYMENT: 'false'
  }
  dependsOn: [
    keyVaultSecretsUser // no effect unless enableKeyVault
    jwtSecret
  ]
}

// Linux "App Service logs > Application logging: File System": the app's console output goes to /home/LogFiles,
// capped at 35 MB (oldest files removed first) and 3 days, so logs can never fill F1's 1 GB of storage.
resource logs 'Microsoft.Web/sites/config@2025-03-01' = {
  parent: site
  name: 'logs'
  properties: {
    httpLogs: {
      fileSystem: {
        enabled: true
        retentionInMb: 35
        retentionInDays: 3
      }
    }
  }
  dependsOn: [appSettings] // one site configuration write at a time
}

// ---------------------------------------------------------------------------------------------------------------
// Front end: Static Web Apps Free
// ---------------------------------------------------------------------------------------------------------------

resource staticWebApp 'Microsoft.Web/staticSites@2025-03-01' = {
  name: names.staticWebApp
  location: staticWebAppLocation
  // Free: 100 GB a month per subscription. Above that the site stops being served; it is never billed. Standard
  // ($9/month) and its enterprise-grade edge add-on are denied by guardrails.bicep.
  sku: { name: 'Free', tier: 'Free' }
  properties: {
    // No pull-request preview environments: their hostnames aren't known in advance, so CORS would block them anyway.
    stagingEnvironmentPolicy: 'Disabled'
  }
}

// ---------------------------------------------------------------------------------------------------------------
// Database: Azure SQL free offer, Entra-only
// ---------------------------------------------------------------------------------------------------------------

resource sqlServer 'Microsoft.Sql/servers@2025-01-01' = {
  name: names.sqlServer
  location: location
  properties: {
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled' // F1 has no VNet integration; access is limited by the firewall and Entra-only auth
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true // no SQL logins or passwords exist at all
      principalType: 'User'
      login: sqlAdminLogin
      sid: sqlAdminObjectId // a USER's object ID (identities use client IDs; see grant-identities.sql)
      tenantId: tenant().tenantId
    }
  }
}

// Entra-only can't be changed through the server's own properties after creation; this resource keeps it enforced.
resource sqlEntraOnly 'Microsoft.Sql/servers/azureADOnlyAuthentications@2025-01-01' = {
  parent: sqlServer
  name: 'Default'
  properties: { azureADOnlyAuthentication: true }
}

// Changing any of these settings later resumes a paused database once (it costs some of the free allowance).
resource database 'Microsoft.Sql/servers/databases@2025-01-01' = {
  parent: sqlServer
  name: names.database
  location: location
  // Writes to one SQL server run one at a time (Entra-only setting, then the database, then the firewall rules).
  // Parallel server-level writes have been reported to fail intermittently with Conflict.
  dependsOn: [sqlEntraOnly]
  // General Purpose serverless, Gen5, up to 2 vCores: the shape of Microsoft's own free-offer example. Compute is
  // counted per second of use, and nothing while paused.
  sku: { name: 'GP_S_Gen5', tier: 'GeneralPurpose', family: 'Gen5', capacity: 2 }
  properties: {
    useFreeLimit: true                        // the free offer: 100,000 vCore-seconds and 32 GB every month
    freeLimitExhaustionBehavior: 'AutoPause'  // when it runs out: pause until the 1st. Never 'BillOverUsage' (one-way)
    autoPauseDelay: 60                        // idle minutes before pausing. 15 would use less per wake-up but is unconfirmed for free databases
    minCapacity: json('0.5')                  // the serverless minimum; typed as int in the schema, hence json()
    maxSizeBytes: 34359738368                 // 32 GB, the free storage allowance
    requestedBackupStorageRedundancy: 'Local' // the only option with AutoPause; backup storage is then free
    zoneRedundant: false
  }
}

module appOutboundFirewall 'modules/sql-firewall.bicep' = {
  name: 'sql-firewall-app-outbound'
  params: {
    sqlServerName: sqlServer.name
    // Every address the app may connect from. They are shared with other apps on the same App Service stamp, but F1
    // has no VNet integration or NAT gateway, so this is the narrowest option there is. IPv6 entries are dropped,
    // because SQL firewall rules are IPv4.
    ipAddresses: filter(split(site.properties.possibleOutboundIpAddresses, ','), ip => !contains(ip, ':'))
  }
  dependsOn: [database] // one SQL server write at a time (see database)
}

// ---------------------------------------------------------------------------------------------------------------
// What the GitHub deploy identity may do
// ---------------------------------------------------------------------------------------------------------------

// Deploy the API package (Kudu OneDeploy with an Entra token). Scoped to this one web app.
resource deployCanPublishApi 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(site.id, deployIdentity.id, websiteContributorRoleId)
  scope: site
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', websiteContributorRoleId)
    principalId: deployIdentity.properties.principalId
    principalType: 'ServicePrincipal' // avoids intermittent failures while a new identity replicates
  }
}

// Open and close its temporary firewall rule (custom role from guardrails.bicep). Scoped to this one SQL server.
resource sqlFirewallOperator 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: guid(subscription().id, 'plantops-sql-firewall-operator')
}

resource deployCanOpenSqlFirewall 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(sqlServer.id, deployIdentity.id, sqlFirewallOperator.id)
  scope: sqlServer
  properties: {
    roleDefinitionId: sqlFirewallOperator.id
    principalId: deployIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------------------------------------------
// Optional Key Vault for the JWT key (enableKeyVault, default false)
// ---------------------------------------------------------------------------------------------------------------

resource keyVault 'Microsoft.KeyVault/vaults@2026-02-01' = if (enableKeyVault) {
  name: names.keyVault
  location: location
  properties: {
    tenantId: tenant().tenantId
    sku: { family: 'A', name: 'standard' } // no monthly fee; about $0.03 per 10,000 operations
    enableRbacAuthorization: true // set explicitly; the schema text and the docs disagree on the default
    softDeleteRetentionInDays: 7  // the minimum; a deleted vault's name stays reserved this long
    // enablePurgeProtection is deliberately omitted: it is irreversible and would lock the name for the full retention.
    publicNetworkAccess: 'Enabled' // F1 has no VNet integration to reach a private vault
  }
}

resource jwtSecret 'Microsoft.KeyVault/vaults/secrets@2026-02-01' = if (enableKeyVault) {
  parent: keyVault
  name: jwtSecretName
  properties: { value: jwtSigningKey }
}

resource keyVaultSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (enableKeyVault) {
  name: guid(keyVault.id, appIdentity.id, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: appIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------------------------------------------
// Outputs: identifiers only. Never the JWT key or the SWA deployment token (deployment outputs are readable by
// anyone with Reader on the resource group).
// ---------------------------------------------------------------------------------------------------------------

output resourceGroupName string = resourceGroup().name
output subscriptionId string = subscription().subscriptionId
output tenantId string = tenant().tenantId
output apiAppName string = site.name
output apiBaseUrl string = 'https://${site.properties.defaultHostName}'
output staticWebAppName string = staticWebApp.name
output webUrl string = 'https://${staticWebApp.properties.defaultHostname}'
output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = database.name
output appIdentityName string = appIdentity.name
output appIdentityClientId string = appIdentity.properties.clientId
output deployIdentityName string = deployIdentity.name
output deployIdentityClientId string = deployIdentity.properties.clientId

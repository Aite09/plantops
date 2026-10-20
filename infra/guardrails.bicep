// Subscription-wide guardrails for PlantOps on Azure for Students. Deployed by hand by the subscription Owner
// (infra/README.md, step 5), and again whenever guardrails.bicepparam changes:
//   az deployment sub create --name plantops-guardrails --location <region> --parameters infra/guardrails.bicepparam
//
// Azure for Students doesn't support Cost Management budgets, and a budget only emails after money is spent anyway.
// These are preventive controls instead. Every policy is a "deny": Azure Resource Manager refuses a request that would
// create something billable before it exists, whoever sends it (Bicep, CLI or a portal click). Azure Policy and
// custom roles are free.
targetScope = 'subscription'

@description('Regions where F1 was listed at 0.0 by the Retail Prices API (README step 4d). F1 is not free everywhere: indonesiacentral bills $0.01 an hour.')
param freeAppServiceLocations string[]

@description('Allow Key Vault at all. Must be true before main.bicep is deployed with enableKeyVault = true; while false, no vault can be created anywhere in the subscription.')
param allowKeyVault bool = false

// Everything the $0 design uses. Anything else (VMs, storage accounts, Application Insights, Log Analytics, public IPs,
// App Service certificates and domains, elastic pools...) is refused. Adding a type here is a deliberate, reviewed
// change: check its price first. Key Vault has no always-free tier on this offer (operations, key rotations and
// certificate renewals are billed per use), so it is allowed only while the flag is on.
var allowedResourceTypes = concat([
  'Microsoft.Resources/subscriptions/resourceGroups'
  'Microsoft.Web/serverFarms'
  'Microsoft.Web/sites'
  'Microsoft.Web/staticSites'
  'Microsoft.Sql/servers'
  'Microsoft.Sql/servers/databases'
  'Microsoft.ManagedIdentity/userAssignedIdentities'
], allowKeyVault ? ['Microsoft.KeyVault/vaults', 'Microsoft.KeyVault/vaults/secrets'] : [])

var guardrails = [
  {
    name: 'plantops-allowed-resource-types'
    displayName: 'PlantOps: only the resource types of the $0 design'
    // Indexed: only resource types that support tags and location are checked, so child settings (site config,
    // firewall rules, federated credentials, role assignments) don't each need listing.
    mode: 'Indexed'
    message: 'PlantOps guardrail: this resource type is not part of the $0 design. Check its price, then add it to allowedResourceTypes in infra/guardrails.bicep.'
    rule: {
      if: { field: 'type', notIn: allowedResourceTypes }
      then: { effect: 'deny' }
    }
  }
  {
    name: 'plantops-app-service-free-only'
    displayName: 'PlantOps: App Service plans must be F1 (Free), in a region where F1 is $0'
    mode: 'All'
    message: 'PlantOps guardrail: only the F1 (Free) App Service plan is allowed, and only in the region(s) checked at $0 (freeAppServiceLocations in infra/guardrails.bicepparam). Every other plan, B1 included, bills from the first hour, and F1 itself is billed in some regions.'
    rule: {
      if: {
        allOf: [
          { field: 'type', equals: 'Microsoft.Web/serverfarms' }
          {
            anyOf: [
              // A missing SKU also matches, so a plan must name F1 explicitly.
              { field: 'Microsoft.Web/serverfarms/sku.name', notEquals: 'F1' }
              // The same condition as Microsoft's built-in "Allowed locations" policy.
              { field: 'location', notIn: freeAppServiceLocations }
            ]
          }
        ]
      }
      then: { effect: 'deny' }
    }
  }
  {
    name: 'plantops-static-web-apps-free-only'
    displayName: 'PlantOps: Static Web Apps must use the Free plan'
    mode: 'All'
    message: 'PlantOps guardrail: only the Free Static Web Apps plan is allowed (Standard costs $9 a month per app).'
    rule: {
      if: {
        allOf: [
          { field: 'type', equals: 'Microsoft.Web/staticSites' }
          { field: 'Microsoft.Web/staticSites/sku.name', notEquals: 'Free' }
        ]
      }
      then: { effect: 'deny' }
    }
  }
  {
    name: 'plantops-sql-free-offer-only'
    displayName: 'PlantOps: SQL databases must use the free offer and pause when it runs out'
    mode: 'All'
    message: 'PlantOps guardrail: SQL databases must set useFreeLimit = true and freeLimitExhaustionBehavior = AutoPause. BillOverUsage bills the overage and can never be switched back.'
    rule: {
      if: {
        allOf: [
          { field: 'type', equals: 'Microsoft.Sql/servers/databases' }
          // The platform creates "master" itself on every server; it isn't billed.
          { field: 'name', notEquals: 'master' }
          {
            // A missing value also matches, so every database must state both settings explicitly. Booleans are
            // compared as text, as in Microsoft's built-in policies.
            anyOf: [
              { field: 'Microsoft.Sql/servers/databases/useFreeLimit', notEquals: 'true' }
              { field: 'Microsoft.Sql/servers/databases/freeLimitExhaustionBehavior', notEquals: 'AutoPause' }
            ]
          }
        ]
      }
      then: { effect: 'deny' }
    }
  }
  {
    name: 'plantops-no-paid-defender'
    displayName: 'PlantOps: no paid Microsoft Defender plans'
    mode: 'All'
    message: 'PlantOps guardrail: Microsoft Defender plans are charged automatically after a 30-day trial (Defender for SQL bills per server). Not part of the $0 design.'
    rule: {
      if: {
        anyOf: [
          {
            allOf: [
              { field: 'type', equals: 'Microsoft.Security/pricings' }
              { field: 'Microsoft.Security/pricings/pricingTier', equals: 'Standard' }
            ]
          }
          {
            // The "Enable Microsoft Defender for SQL" button on the SQL server's portal pages.
            allOf: [
              { field: 'type', equals: 'Microsoft.Sql/servers/advancedThreatProtectionSettings' }
              { field: 'Microsoft.Sql/servers/advancedThreatProtectionSettings/state', equals: 'Enabled' }
            ]
          }
          {
            // The older API for the same server setting, still current; Microsoft's built-in policy "Configure Azure
            // Defender to be enabled on SQL servers" turns Defender on through it.
            allOf: [
              { field: 'type', equals: 'Microsoft.Sql/servers/securityAlertPolicies' }
              { field: 'Microsoft.Sql/servers/securityAlertPolicies/state', equals: 'Enabled' }
            ]
          }
        ]
      }
      then: { effect: 'deny' }
    }
  }
]

resource definitions 'Microsoft.Authorization/policyDefinitions@2025-03-01' = [
  for guardrail in guardrails: {
    name: guardrail.name
    properties: {
      policyType: 'Custom'
      mode: guardrail.mode
      displayName: guardrail.displayName
      description: guardrail.message
      metadata: { category: 'PlantOps' }
      policyRule: guardrail.rule
    }
  }
]

resource assignments 'Microsoft.Authorization/policyAssignments@2025-03-01' = [
  for (guardrail, i) in guardrails: {
    name: guardrail.name
    properties: {
      displayName: guardrail.displayName
      policyDefinitionId: definitions[i].id
      enforcementMode: 'Default' // actually denies ('DoNotEnforce' would only report)
      nonComplianceMessages: [{ message: guardrail.message }] // shown in the error, so a refusal explains itself
    }
  }
]

// Least privilege for the GitHub deploy job: open and close its own temporary firewall rule, nothing else.
// main.bicep assigns this role on the PlantOps SQL server only. The built-in roles that could do it (SQL Server
// Contributor, SQL Security Manager) could also change the database's billing settings or turn on Defender.
resource sqlFirewallOperator 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(subscription().id, 'plantops-sql-firewall-operator') // main.bicep refers to the same name
  properties: {
    roleName: 'PlantOps SQL Firewall Operator (${take(uniqueString(subscription().id), 6)})' // unique in the tenant
    description: 'Read a SQL server and create or delete its firewall rules. Used by the PlantOps deploy job.'
    type: 'CustomRole'
    assignableScopes: [subscription().id]
    permissions: [
      {
        actions: [
          'Microsoft.Sql/servers/read'
          'Microsoft.Sql/servers/firewallRules/read'
          'Microsoft.Sql/servers/firewallRules/write'
          'Microsoft.Sql/servers/firewallRules/delete'
        ]
        notActions: []
      }
    ]
  }
}

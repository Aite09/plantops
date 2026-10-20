using 'guardrails.bicep'

// The API region from main.bicepparam, after README step 4d showed F1 Linux at 0.0 there. Policy then refuses an F1
// plan anywhere else, so a tutorial or portal click can't create one in a region where F1 is billed.
param freeAppServiceLocations = ['centralus']

// Must equal main.bicepparam's enableKeyVault. Set it to true and redeploy this file BEFORE turning Key Vault on.
param allowKeyVault = false

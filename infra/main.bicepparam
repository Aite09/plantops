using 'main.bicep'

// Chosen in README step 4: allowed by the subscription's region policy, F1 Linux listed at $0.00, serverless SQL
// available. The free SQL database's region can never change for this subscription, so choose once.
param location = 'centralus'
// Static Web Apps Free exists in only a few regions (README step 4). It doesn't have to match the API's region.
param staticWebAppLocation = 'centralus'

// GitHub's OIDC subject for jobs that use the 'production' environment. Repositories created after 2026-07-15 use the
// immutable format with the owner and repository IDs (check: GET /repos/Aite09/plantops/actions/oidc/customization/sub).
param githubOidcSubject = 'repo:Aite09@130272673/plantops@1426190802:environment:production'

// Off: the JWT key stays an app setting. To turn on, first set allowKeyVault = true in guardrails.bicepparam and
// redeploy it (README step 5), then set this to true and rerun README step 6.
param enableKeyVault = false

// Read from the shell at deploy time, so personal identifiers and the secret never land in the repo (README step 6).
param sqlAdminLogin = readEnvironmentVariable('PLANTOPS_SQL_ADMIN_LOGIN')
param sqlAdminObjectId = readEnvironmentVariable('PLANTOPS_SQL_ADMIN_OBJECT_ID')
param jwtSigningKey = readEnvironmentVariable('PLANTOPS_JWT_SIGNING_KEY')

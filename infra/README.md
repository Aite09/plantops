# PlantOps on Azure: one-time setup

How the public demo is deployed at $0 on Azure for Students, and the steps to set it up from scratch. The design in
brief:

- **API** on App Service F1 (Linux, .NET 10), **front end** on Static Web Apps Free, **database** on the Azure SQL free
  offer (serverless, pauses instead of billing when the monthly allowance runs out).
- **No passwords**: SQL is Entra-only. The API connects as a user-assigned managed identity; GitHub deploys through
  OIDC with a federated credential bound to this repository's `production` environment. The only secret is the
  Static Web Apps deployment token.
- **$0 is enforced**: subscription-level Azure Policy deny rules allow only F1 (in the region checked at $0), SWA Free,
  the SQL free offer with AutoPause, no paid Defender, and only the resource types this design uses.

| File | What it is |
| --- | --- |
| [`guardrails.bicep`](guardrails.bicep) + [`guardrails.bicepparam`](guardrails.bicepparam) | Subscription scope: the five deny policies and the custom role that lets the deploy job manage only its own SQL firewall rule |
| [`main.bicep`](main.bicep) + [`main.bicepparam`](main.bicepparam) | Resource group scope: identities, App Service plan and web app (with all its app settings), Static Web App, SQL server and database, role assignments, optional Key Vault (off) |
| [`modules/sql-firewall.bicep`](modules/sql-firewall.bicep) | SQL firewall rules for the web app's outbound IP addresses |
| [`sql/grant-identities.sql`](sql/grant-identities.sql) | Contained database users for the two managed identities (run once, step 8) |

Deployments after the setup are automatic: the `deploy` and `deploy-web` jobs in
[`.github/workflows/ci.yml`](../.github/workflows/ci.yml) run on every push to `main` once the three CI checks pass.

> Run everything below **once**, from the repository root in zsh, on the `phase-6-azure` branch, **before** merging it to main.
> Use one terminal session throughout: later steps reuse shell variables (`LOCATION`, `T`, `out`, `R`, ...).
> Cost: every step is $0. Steps that touch the database wake it once and use part of the monthly free allowance of
> 100,000 vCore-seconds; nothing is ever billed.

### Step 1. Tools ($0)

```zsh
brew install azure-cli gh jq sqlcmd    # sqlcmd = Microsoft's go-sqlcmd
az version --query '"azure-cli"' -o tsv # need 2.90 or later (Key Vault API 2026-02-01; current Bicep)
az bicep install && az bicep upgrade
```

### Step 2. Sign in and confirm the subscription ($0)

```zsh
az login
az account show --query "{name:name, subscription:id, tenant:tenantId, user:user.name}" -o table
SUB=$(az account show --query id -o tsv)
# Must print AzureForStudents_2018-01-01 (Azure for Students, offer 0170P). For anything else, stop: the cost
# analysis assumes this offer (credit-limited, no card, so it is disabled rather than billed).
az rest --method get --url "https://management.azure.com/subscriptions/$SUB?api-version=2022-12-01" \
  --query subscriptionPolicies.quotaId -o tsv
# Must list Owner (needed for policies, custom roles and the SQL Entra admin).
az role assignment list --assignee "$(az ad signed-in-user show --query id -o tsv)" --scope "/subscriptions/$SUB" \
  --query "[].roleDefinitionName" -o tsv
```

### Step 3. Register resource providers ($0)

```zsh
for ns in Microsoft.Web Microsoft.Sql Microsoft.ManagedIdentity Microsoft.KeyVault; do
  az provider register --namespace "$ns" --wait
done
```

### Step 4. Choose the regions (read-only, $0)

```zsh
# a. Regions your Students subscription allows (about 5; they differ per student). Look for the
#    "Allowed resource deployment regions" assignment and its allowed locations list.
az policy assignment list --disable-scope-strict-match --query "[].{name:displayName, parameters:parameters}" -o json

# b. Regions where Static Web Apps exists (display names; "West US 2" = westus2).
az provider show --namespace Microsoft.Web --query "resourceTypes[?resourceType=='staticSites'].locations | [0]" -o tsv

# c. Regions that offer F1 on Linux.
az appservice list-locations --sku F1 --linux-workers-enabled --query "[].name" -o tsv

# d. For your candidate API region: F1 Linux must cost 0.0 (it does not everywhere; indonesiacentral is $0.01/hour).
LOCATION=centralus   # your candidate
curl -sG https://prices.azure.com/api/retail/prices \
  --data-urlencode "\$filter=productName eq 'Azure App Service Free Plan - Linux' and armRegionName eq '$LOCATION'" \
  | jq '.Items[] | {armRegionName, meterName, retailPrice}'

# e. Serverless SQL in that region.
az sql db list-editions --location "$LOCATION" --edition GeneralPurpose --service-objective GP_S_Gen5_2 -o table
```

**Pick the regions:**
- `location`: a region in a ∩ c ∩ e whose F1 price in d is 0.0.
- `staticWebAppLocation`: a region in a ∩ b.
- Optional: check F1 quota in the portal under **Quotas › App Service** (filter by region; "Total VMs" must not be 0).
  A zero quota just makes step 6 fail with `SubscriptionIsOverQuotaForSku`, at no cost.
- Edit both values in `infra/main.bicepparam`, put the same `location` in `freeAppServiceLocations` in
  `infra/guardrails.bicepparam`, and commit.

**GATE: if a ∩ b is empty, stop.** Static Web Apps can't be created in this subscription; Students moderators say the
region policy can't be overridden. That is a decision for the owner, with GitHub Pages or serving the SPA from the F1
app as the alternatives. Do not change SKUs to get around it.

### Step 5. Guardrails, then prove they deny ($0)

```zsh
# Reads infra/guardrails.bicepparam: freeAppServiceLocations must hold the region you checked in step 4d.
az deployment sub create --name plantops-guardrails --location "$LOCATION" \
  --parameters infra/guardrails.bicepparam --output none
az group create --name rg-plantops --location "$LOCATION" --output none
```

Wait about 15 minutes for the assignments to take effect. Then run these **preflight validations**. They evaluate Azure
Policy but never create anything, so even a policy that isn't active yet costs nothing.

```zsh
SWA_LOCATION=centralus     # the staticWebAppLocation you chose
OTHER_LOCATION=eastus2     # any OTHER region from step 4a's allowed list (not $LOCATION)
T="${TMPDIR:-/tmp}/plantops-guardrail-test.bicep"
cat > "$T" <<'EOF'
// NEVER DEPLOY THIS FILE. It exists only for `az deployment group validate`.
@allowed(['appServicePlan', 'freePlanElsewhere', 'staticWebApp', 'keyVault', 'sqlDatabase'])
param case string
param swaLocation string = resourceGroup().location
param otherLocation string = resourceGroup().location
param sqlServerName string = 'unused'

resource paidPlan 'Microsoft.Web/serverfarms@2025-03-01' = if (case == 'appServicePlan') {
  name: 'guardrail-test-b1'
  location: resourceGroup().location
  kind: 'linux'
  sku: { name: 'B1', tier: 'Basic' }
  properties: { reserved: true }
}

resource freePlanElsewhere 'Microsoft.Web/serverfarms@2025-03-01' = if (case == 'freePlanElsewhere') {
  name: 'guardrail-test-f1-elsewhere'
  location: otherLocation
  kind: 'linux'
  sku: { name: 'F1', tier: 'Free' }
  properties: { reserved: true }
}

resource paidStaticWebApp 'Microsoft.Web/staticSites@2025-03-01' = if (case == 'staticWebApp') {
  name: 'guardrail-test-standard'
  location: swaLocation
  sku: { name: 'Standard', tier: 'Standard' }
  properties: {}
}

resource vaultWhileFlagIsOff 'Microsoft.KeyVault/vaults@2026-02-01' = if (case == 'keyVault') {
  name: 'kv-guardrail-${take(uniqueString(resourceGroup().id), 8)}'
  location: resourceGroup().location
  properties: { tenantId: tenant().tenantId, sku: { family: 'A', name: 'standard' }, enableRbacAuthorization: true }
}

resource sqlServer 'Microsoft.Sql/servers@2025-01-01' existing = {
  name: sqlServerName
}

resource billedDatabase 'Microsoft.Sql/servers/databases@2025-01-01' = if (case == 'sqlDatabase') {
  parent: sqlServer
  name: 'guardrail-test'
  location: resourceGroup().location
  sku: { name: 'GP_S_Gen5', tier: 'GeneralPurpose', family: 'Gen5', capacity: 2 }
  properties: { useFreeLimit: true, freeLimitExhaustionBehavior: 'BillOverUsage' }
}
EOF
for c in appServicePlan freePlanElsewhere staticWebApp keyVault; do
  echo "== $c"
  az deployment group validate --resource-group rg-plantops --template-file "$T" \
    --parameters case=$c swaLocation="$SWA_LOCATION" otherLocation="$OTHER_LOCATION" \
    --output none 2>&1 | grep -o 'plantops-[a-z-]*' | sort -u
done
```

**Expected output:**
- `plantops-app-service-free-only` for `appServicePlan` (wrong SKU).
- `plantops-app-service-free-only` for `freePlanElsewhere` (F1, but outside `freeAppServiceLocations`).
- `plantops-static-web-apps-free-only` for `staticWebApp`.
- `plantops-allowed-resource-types` for `keyVault` (while `allowKeyVault` is false).

The positive case, F1 in `$LOCATION`, is proved by step 6 itself.

No output means the policy isn't active yet. Wait and rerun; nothing was created. The SQL case runs in step 8, once
the server exists.

### Step 6. Deploy the infrastructure ($0)

```zsh
export PLANTOPS_SQL_ADMIN_LOGIN=$(az ad signed-in-user show --query userPrincipalName -o tsv)
export PLANTOPS_SQL_ADMIN_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)
# The JWT key exists only for this one command, then only in the app's settings. Every rerun rotates it.
PLANTOPS_JWT_SIGNING_KEY="$(openssl rand -base64 48)" \
  az deployment group create --resource-group rg-plantops --name plantops-main \
  --parameters infra/main.bicepparam --output none
```

- **Policy denial.** If this is refused with `RequestDisallowedByPolicy` naming `plantops-sql-free-offer-only` even
  though the template sets both values, the boolean comparison behaves differently than documented. In
  `guardrails.bicep`, change `notEquals: 'true'` to `notEquals: true`, redeploy it, wait, and retry.
- **Unique hostname.** If creating the web app fails over `autoGeneratedDomainNameLabelScope` (F1 support for it is not
  documented), delete that line from `main.bicep` and rerun. The app then gets `app-plantops-<suffix>.azurewebsites.net`.
  The suffix is still unpredictable, but the hostname is not protected against reuse after deletion.
- **Conflict on a `Microsoft.Sql` resource.** The SQL writes are already serialized (`dependsOn`), but if one still
  fails with `Conflict`, rerun the same command. Every resource is idempotent.

### Step 7. Read the outputs (no secrets in them)

```zsh
OUTPUTS=$(az deployment group show --resource-group rg-plantops --name plantops-main --query properties.outputs -o json)
out() { jq -r --arg k "$1" '.[$k].value' <<< "$OUTPUTS"; }
jq -r 'to_entries[] | "\(.key) = \(.value.value)"' <<< "$OUTPUTS"
```

### Step 8. Database users for the two identities ($0; wakes the database once)

```zsh
SQL_SERVER=$(out sqlServerName); SQL_FQDN=$(out sqlServerFqdn); SQL_DB=$(out sqlDatabaseName)
MY_IP=$(curl -fsS https://api.ipify.org)
az sql server firewall-rule create --resource-group rg-plantops --server "$SQL_SERVER" --name bootstrap-owner \
  --start-ip-address "$MY_IP" --end-ip-address "$MY_IP" --output none
# A new rule can take a few minutes to apply (error 40615, client IP not allowed, until then), so this retries.
# The script is safe to run again. The rule is removed afterwards either way.
for attempt in {1..10}; do
  sqlcmd -S "tcp:$SQL_FQDN,1433" -d "$SQL_DB" --authentication-method ActiveDirectoryDefault -b \
    -i infra/sql/grant-identities.sql \
    -v AppIdentityName="$(out appIdentityName)" -v AppClientId="$(out appIdentityClientId)" \
    -v DeployIdentityName="$(out deployIdentityName)" -v DeployClientId="$(out deployIdentityClientId)" && break
  (( attempt == 10 )) && { echo "Still failing after 10 attempts: read the error above."; break; }
  echo "Not in yet; retrying in 30 s"; sleep 30
done
az sql server firewall-rule delete --resource-group rg-plantops --server "$SQL_SERVER" --name bootstrap-owner
# Check that each sid_as_client_id equals the matching client ID:
echo "app: $(out appIdentityClientId)   deploy: $(out deployIdentityClientId)"

# Guardrail check for SQL: preflight only, nothing is created. Expected output: plantops-sql-free-offer-only
az deployment group validate --resource-group rg-plantops --template-file "$T" \
  --parameters case=sqlDatabase sqlServerName="$SQL_SERVER" --output none 2>&1 | grep -o 'plantops-[a-z-]*' | sort -u
```

The portal's **Query editor** works too: open the database, sign in as yourself, and paste the script with the four
`$(...)` values filled in. It offers to add your IP to the firewall; delete that rule afterwards.

### Step 9. GitHub environment, variables and the one secret ($0)

```zsh
gh auth login --hostname github.com --git-protocol https --web
R=Aite09/plantops

# The OIDC subject format: expect "use_immutable_subject": true and the same prefix as githubOidcSubject.
curl -fsS "https://api.github.com/repos/$R/actions/oidc/customization/sub"

# Environment "production", deployable from main only. This branch policy decides which code can get an Azure token.
gh api --method PUT "repos/$R/environments/production" --input - <<'EOF'
{"deployment_branch_policy": {"protected_branches": false, "custom_branch_policies": true}}
EOF
gh api --method POST "repos/$R/environments/production/deployment-branch-policies" -f name=main -f type=branch

# Variables: identifiers and the public demo password. None is a credential.
setvar() { gh variable set "$1" --env production --repo "$R" --body "$2"; }
setvar AZURE_CLIENT_ID       "$(out deployIdentityClientId)"
setvar AZURE_TENANT_ID       "$(out tenantId)"
setvar AZURE_SUBSCRIPTION_ID "$(out subscriptionId)"
setvar AZURE_RESOURCE_GROUP  "$(out resourceGroupName)"
setvar API_APP_NAME          "$(out apiAppName)"
setvar API_BASE_URL          "$(out apiBaseUrl)"
setvar WEB_URL               "$(out webUrl)"
setvar SQL_SERVER_NAME       "$(out sqlServerName)"
setvar SQL_SERVER_FQDN       "$(out sqlServerFqdn)"
setvar SQL_DATABASE_NAME     "$(out sqlDatabaseName)"
# Public by design (it is printed on the login page). It meets Identity's policy: 10+ characters, upper, lower,
# digit, symbol. It contains no "!", which zsh would expand inside double quotes.
setvar DEMO_PASSWORD         "Demo-$(openssl rand -hex 4)-Pw1"

# The only secret: the SWA deployment token. Piped straight from Azure into GitHub, never printed.
SWA_TOKEN=$(az staticwebapp secrets list --name "$(out staticWebAppName)" --resource-group rg-plantops \
  --query properties.apiKey -o tsv)
if [[ -n "$SWA_TOKEN" ]]; then
  printf '%s' "$SWA_TOKEN" | gh secret set AZURE_STATIC_WEB_APPS_API_TOKEN --env production --repo "$R"
else
  # Prints only property NAMES, never values. Use the name that holds the token in the command above.
  az staticwebapp secrets list --name "$(out staticWebAppName)" --resource-group rg-plantops --query "keys(properties)"
fi
unset SWA_TOKEN
```

Optional: require your own approval before each production deploy. This is free on public repos. Both deploy jobs use
the environment, so each run asks twice (API, then front end). Leave `prevent_self_review` false, or nobody could
approve:

```zsh
gh api --method PUT "repos/$R/environments/production" --input - <<'EOF'
{"reviewers": [{"type": "User", "id": 130272673}], "prevent_self_review": false,
 "deployment_branch_policy": {"protected_branches": false, "custom_branch_policies": true}}
EOF
```

### Step 10. Rulesets on main ($0 on public repos)

Two rulesets:
- **Integrity.** Nobody, including you, can force-push to or delete `main`.
- **Checks.** Pull requests need the three CI checks green. You, as repository admin, can bypass it (logged), so your
  current "fast-forward main and push" workflow keeps working. Deploys stay gated either way, because the `deploy` job
  `needs:` all three checks in the same run, and `deploy-web` needs `deploy`.

```zsh
gh api --method POST "repos/$R/rulesets" --input - <<'EOF'
{"name": "main: no force-push or deletion", "target": "branch", "enforcement": "active",
 "conditions": {"ref_name": {"include": ["~DEFAULT_BRANCH"], "exclude": []}},
 "bypass_actors": [],
 "rules": [{"type": "deletion"}, {"type": "non_fast_forward"}]}
EOF

gh api --method POST "repos/$R/rulesets" --input - <<'EOF'
{"name": "main: pull request with green CI", "target": "branch", "enforcement": "active",
 "conditions": {"ref_name": {"include": ["~DEFAULT_BRANCH"], "exclude": []}},
 "bypass_actors": [{"actor_id": 5, "actor_type": "RepositoryRole", "bypass_mode": "always"}],
 "rules": [
  {"type": "pull_request", "parameters": {"required_approving_review_count": 0, "dismiss_stale_reviews_on_push": false,
   "require_code_owner_review": false, "require_last_push_approval": false, "required_review_thread_resolution": false,
   "allowed_merge_methods": ["merge", "squash", "rebase"]}},
  {"type": "required_status_checks", "parameters": {"strict_required_status_checks_policy": true,
   "do_not_enforce_on_create": false, "required_status_checks": [
    {"context": "Backend (build, test, coverage)", "integration_id": 15368},
    {"context": "Frontend (test, build, audit)", "integration_id": 15368},
    {"context": "Docker images", "integration_id": 15368}]}}]}
EOF
# Check that the bypass actor is the Admin role (actor_id 5 is the commonly used value, but not documented):
# the settings page should show "Repository admin" under Bypass list.
gh api "repos/$R/rulesets" --jq '.[] | {id, name}'
```

`required_approving_review_count` is 0 because a solo owner can't approve their own pull request. The check names are
the jobs' `name:` values, and `integration_id` 15368 is GitHub Actions.

### Step 11. First deployment and checks ($0)

Merge or fast-forward the `phase-6-azure` branch to `main` and push. Then:

```zsh
gh run watch --repo "$R"   # the first run has no previous deployment, so it migrates and seeds
open "$(out webUrl)"        # sign in as staff@plantops.test with the password shown on the page
```

**After the first deploy, check:**
1. The `azure/login` step log, under "Federated token details", shows a subject identical to `githubOidcSubject`.
2. The firewall has no `gh-*` or `bootstrap-*` rules left:
   `az sql server firewall-rule list -g rg-plantops -s "$SQL_SERVER" --query "[].name" -o tsv`.
   Only `app-outbound-*` rules should remain.
3. Free allowance left, read from Azure Monitor (doesn't wake the database):
   ```zsh
   az monitor metrics list --resource "$(az sql db show -g rg-plantops -s "$SQL_SERVER" -n "$SQL_DB" --query id -o tsv)" \
     --metric free_amount_remaining --interval PT1H -o table
   ```
4. Each visitor gets their own login rate limit (forwarded headers), and a client can't fake its way out of it:
   - From the laptop, send 11 rapid logins for a non-existent email, each claiming a different client IP:
     ```zsh
     for i in {1..11}; do curl -s -o /dev/null -w "%{http_code} " -X POST "$(out apiBaseUrl)/api/auth/login" \
       -H 'Content-Type: application/json' -H "X-Forwarded-For: 198.51.100.$i" \
       -d '{"email":"nobody@example.test","password":"x"}'; done; echo
     ```
     The 11th must return `429`. Ten `401`s followed by a `429` mean the limiter keys on the address App Service's
     front end saw, not on the header the client sent.
   - Within the same minute, from a phone on mobile data, sign in at the public site. It must still work (the laptop's
     limit is not shared with other visitors).
   - **If the 11th is not `429`**, the client-supplied header wins and anyone could bypass the login limit. Switch to
     App Service's `client-ip` header, which its front end always overwrites (app-service-linux-docs, headers.md). Add
     one line to `Program.cs`, before `builder.Build()`:
     `builder.Services.Configure<ForwardedHeadersOptions>(options => options.ForwardedForHeaderName = "Client-IP");`
     with `using Microsoft.AspNetCore.HttpOverrides;` (the app setting still turns the middleware on). In
     `LoginRateLimitingTests.LoginFromAsync`, send `Client-IP` instead of `X-Forwarded-For`. Redeploy and repeat this
     check.

### Step 12. Optional: create a first Admin (owner's machine only, $0; wakes the database once)

**Use a dedicated address**, not your personal or work email and not one of the `@plantops.test` demo accounts (the
command refuses those: their password is public). The Admin's email is its sign-in name. The user list (`src/PlantOps.Application/Users/ListUsers.cs`) hides Admin
accounts from Supervisors, but a non-obvious address also stops anyone from locking the Admin out by guessing it.

```zsh
MY_IP=$(curl -fsS https://api.ipify.org)
az sql server firewall-rule create -g rg-plantops -s "$SQL_SERVER" -n bootstrap-owner \
  --start-ip-address "$MY_IP" --end-ip-address "$MY_IP" -o none
read "ADMIN_EMAIL?Admin email (a dedicated address): "
# Generated, never typed or printed. The suffix guarantees Identity's upper, lower, digit and symbol rules.
ADMIN_PASSWORD="$(openssl rand -base64 24)-Aa1"
printf '%s' "$ADMIN_PASSWORD" | pbcopy
read "?The password is on the clipboard. Save it in your password manager, then press Enter. "
pbcopy < /dev/null
# Retries while the new firewall rule applies (error 40615); the command is safe to run again.
for attempt in {1..10}; do
  ASPNETCORE_ENVIRONMENT=Production \
  ConnectionStrings__PlantOps="Server=tcp:$SQL_FQDN,1433;Database=$SQL_DB;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connect Timeout=60" \
  Bootstrap__AdminEmail="$ADMIN_EMAIL" Bootstrap__AdminPassword="$ADMIN_PASSWORD" \
    dotnet run --project src/PlantOps.Api --no-launch-profile -- create-admin && break
  (( attempt == 10 )) && { echo "Still failing after 10 attempts: read the error above."; break; }
  echo "Not in yet; retrying in 30 s"; sleep 30
done
unset ADMIN_PASSWORD
az sql server firewall-rule delete -g rg-plantops -s "$SQL_SERVER" -n bootstrap-owner
```

`--no-launch-profile` matters: the launch profile would set Development, which loads your local user-secrets.
`Active Directory Default` signs in as you, the Entra admin. The password exists only in this shell, your password
manager and, afterwards, as Identity's hash. Running this step again for the same email sets the newly generated
password, so it also recovers a forgotten Admin password.

### Step 13. Routine operations

| Task | Command | Cost |
| --- | --- | --- |
| Change infrastructure | Edit `infra/main.bicep` or `main.bicepparam`, then rerun step 6 (rotates the JWT key; signed-in users sign in again) | $0; may wake the database once |
| Change guardrails | Edit `infra/guardrails.bicep` or `guardrails.bicepparam`, then rerun the first command of step 5 and its preflight checks | $0 |
| Turn Key Vault on (optional) | `allowKeyVault = true` in `guardrails.bicepparam` → step 5's first command → wait ~15 min → `enableKeyVault = true` in `main.bicepparam` → step 6 | Fractions of a cent a month in operations (not provably $0) |
| Check the database on demand | `gh workflow run ci.yml --repo $R --ref main -f run_database_step=true` (also runs the readiness check) | $0; wakes the database once |
| Change the public demo password | `gh variable set DEMO_PASSWORD --env production --repo $R --body "<new>"`, then `gh workflow run ci.yml --repo $R --ref main -f run_database_step=true` | $0 |
| Rotate the SWA token | `az staticwebapp secrets reset-api-key --name <swa> -g rg-plantops`, then redo the secret part of step 9 | $0 |
| Inactivity | Sign in to the portal at least every 60 days. The Students terms allow removing resources after 90 days of inactivity, and whether CI's API calls count is unconfirmed. Check the credit balance at https://www.microsoftazuresponsorships.com/ | $0 |
| Stale app firewall rules (rare; only if the app's outbound IPs change) | `az sql server firewall-rule list ...` then delete `app-outbound-*` rules whose IP is no longer in `az webapp show -g rg-plantops -n <app> --query possibleOutboundIpAddresses` | $0 |
| Tear down | `az group delete --name rg-plantops` (the free-database slot frees within about an hour). With Key Vault on, also run `az keyvault purge --name <vault>` or wait 7 days before reusing the name. | $0 |

### Never click in the portal

Each of these bills, burns the $100 credit, or permanently removes a $0 guarantee. The guardrails deny most of them;
don't rely on that.

- App Service plan: **Scale up** (any tier above F1), and any "Upgrade" or "Recommended" banner.
- Web app: **Application Insights › Turn on**, **Diagnostic settings › Send to Log Analytics / storage**, **Custom
  domains › App Service domain**, **Certificates / TLS** purchase, and **Deployment Center › GitHub Actions**. That last
  one writes its own workflow and secret and overwrites this design.
- SQL database: **Compute + storage** (any change resumes the database; "Continue using database for additional
  charges" bills and can never be reverted), **Behavior when free limit reached**, **Geo-replication**, **Long-term
  retention**, **Auditing**.
- SQL server: **Microsoft Defender for SQL / Start free trial** (charged automatically after 30 days).
- SQL blades that wake a paused database: Query Store, Auditing, TDE status, Dynamic data masking, Data
  classification, Performance recommendations. Visiting them uses free allowance, not money.
- Static Web App: **Hosting plan › Standard**, **Enterprise-grade edge**.
- Key Vault (only if you turned it on): **Premium** tier, HSM-protected keys, **Rotation policy**, **Certificates**.
  Each is billed per key, rotation or renewal.
- Subscription: **Upgrade** to Pay-As-You-Go. That removes the spending limit, the backstop of this whole design.
- Microsoft Defender for Cloud: **Enable all plans / Start trial**.

**Never set up a monitor on the database.** Don't point an uptime monitor (UptimeRobot and the like), an Azure
availability test, a pinger or a scheduled workflow at `/health` or any `/api` route. Each call signs in to SQL and
resumes the paused database, and one call an hour keeps it online for good, using up the free allowance in under two
days. Only `/health/live` is safe to poll.

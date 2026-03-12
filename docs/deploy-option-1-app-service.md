# Option 1 — Azure App Service Deployment Plan

## Architecture

```text
┌──────────────────────────────────────────────────────┐
│                   Azure App Service                  │
│                                                      │
│  ┌─────────────────┐       ┌──────────────────────┐  │
│  │  ECTSystem.Web   │       │   ECTSystem.Api       │  │
│  │  (App Service)   │──────▶│   (App Service)       │  │
│  │  Blazor WASM     │ HTTPS │   OData / Identity    │  │
│  └─────────────────┘       └──────────┬───────────┘  │
│                                       │              │
│                                       ▼              │
│                            ┌──────────────────────┐  │
│                            │    Azure SQL          │  │
│                            │    (Entra Auth)       │  │
│                            └──────────────────────┘  │
└──────────────────────────────────────────────────────┘
```

---

## Prerequisites

- Azure subscription with Contributor access
- Azure CLI (`az`) installed
- .NET 10 SDK installed
- GitHub repo connected (for CI/CD)
- Azure SQL Database already provisioned
  (`sql-ect-dev-cus.database.windows.net`)

---

## Phase 1 — Provision Azure Resources

### 1.1 Create Resource Group (if not existing)

```bash
az group create \
  --name rg-ectsystem-prod \
  --location centralus
```

### 1.2 Create App Service Plan

```bash
az appservice plan create \
  --name asp-ectsystem-prod \
  --resource-group rg-ectsystem-prod \
  --sku B1 \
  --is-linux
```

> **Recommendation:** Use Linux for cost savings. Use `S1` or higher for
> deployment slots (required for Blue/Green).

### 1.3 Create Web App for API

```bash
az webapp create \
  --name app-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod \
  --plan asp-ectsystem-prod \
  --runtime "DOTNETCORE:10.0"
```

### 1.4 Create Web App for Blazor WASM Frontend

```bash
az webapp create \
  --name app-ectsystem-web-prod \
  --resource-group rg-ectsystem-prod \
  --plan asp-ectsystem-prod \
  --runtime "DOTNETCORE:10.0"
```

> **Alternative:** Serve the Blazor WASM app as static files from the API
> project using `app.UseBlazorFrameworkFiles()` — reduces to a single App
> Service.

### 1.5 Enable Managed Identity on API App

```bash
az webapp identity assign \
  --name app-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod
```

### 1.6 Grant SQL Access to Managed Identity

```sql
-- Run in Azure SQL (ECT database)
CREATE USER [app-ectsystem-api-prod] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [app-ectsystem-api-prod];
ALTER ROLE db_datawriter ADD MEMBER [app-ectsystem-api-prod];
```

---

## Phase 2 — Configure App Settings

### 2.1 API App Configuration

```bash
az webapp config appsettings set \
  --name app-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    "ConnectionStrings__DefaultConnection=\
Server=sql-ect-dev-cus.database.windows.net;\
Database=ECT;\
Authentication=Active Directory Default;"
```

### 2.2 Web App Configuration

```bash
az webapp config appsettings set \
  --name app-ectsystem-web-prod \
  --resource-group rg-ectsystem-prod \
  --settings \
    "ApiBaseUrl=https://app-ectsystem-api-prod.azurewebsites.net"
```

### 2.3 Configure CORS on the API

```bash
az webapp cors add \
  --name app-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod \
  --allowed-origins "https://app-ectsystem-web-prod.azurewebsites.net"
```

---

## Phase 3 — CI/CD with GitHub Actions

### 3.1 API Deployment Workflow

Create `.github/workflows/deploy-api.yml`:

```yaml
name: Deploy API to Azure App Service

on:
  push:
    branches: [main]
    paths:
      - "ECTSystem.Api/**"
      - "ECTSystem.Persistence/**"
      - "ECTSystem.Shared/**"

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: Build
        run:
          dotnet publish ECTSystem.Api/ECTSystem.Api.csproj -c Release -o
          ./publish

      - name: Deploy to Azure
        uses: azure/webapps-deploy@v3
        with:
          app-name: app-ectsystem-api-prod
          publish-profile: ${{ secrets.AZURE_API_PUBLISH_PROFILE }}
          package: ./publish
```

### 3.2 Web Deployment Workflow

Create `.github/workflows/deploy-web.yml`:

```yaml
name: Deploy Web to Azure App Service

on:
  push:
    branches: [main]
    paths:
      - "ECTSystem.Web/**"
      - "ECTSystem.Shared/**"

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: Build
        run:
          dotnet publish ECTSystem.Web/ECTSystem.Web.csproj -c Release -o
          ./publish

      - name: Deploy to Azure
        uses: azure/webapps-deploy@v3
        with:
          app-name: app-ectsystem-web-prod
          publish-profile: ${{ secrets.AZURE_WEB_PUBLISH_PROFILE }}
          package: ./publish
```

---

## Phase 4 — Post-Deployment Configuration

### 4.1 Enable HTTPS Only

```bash
az webapp update \
  --name app-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod \
  --https-only true
az webapp update \
  --name app-ectsystem-web-prod \
  --resource-group rg-ectsystem-prod \
  --https-only true
```

### 4.2 Configure Custom Domains (Optional)

```bash
az webapp config hostname add \
  --webapp-name app-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod \
  --hostname api.ectsystem.mil

az webapp config ssl bind \
  --name app-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod \
  --certificate-thumbprint <THUMBPRINT> \
  --ssl-type SNI
```

### 4.3 Apply EF Core Migrations

```bash
# Option A: Run from local machine targeting production
ASPNETCORE_ENVIRONMENT=Production dotnet ef database update \
  --project ECTSystem.Persistence \
  --startup-project ECTSystem.Api

# Option B: Add migration step to CI/CD pipeline (preferred)
```

### 4.4 SPA Fallback Routing for Blazor WASM

Add a `web.config` or configure the Linux startup to serve `index.html` for
unmatched routes:

```bash
az webapp config set \
  --name app-ectsystem-web-prod \
  --resource-group rg-ectsystem-prod \
  --startup-file "dotnet ECTSystem.Web.dll"
```

---

## Phase 5 — Blue/Green Deployments

Blue/Green deployment eliminates downtime and reduces deployment risk by
maintaining two identical production environments. Only one (the "active" slot)
serves live traffic at any time. New releases are deployed to the inactive slot,
validated, then swapped into production.

### 5.1 Overview

```text
                    ┌─────────────────────────────┐
                    │      Azure App Service       │
                    │                               │
  Live Traffic ────▶│  Production Slot (Blue)  ◄──── current release v1.2
                    │                               │
                    │  Staging Slot (Green)     ◄──── new release v1.3
                    │      ▲                        │
                    │      │ validate & test         │
                    │      │                        │
                    │  ───── SWAP ─────▶            │
                    │                               │
  Live Traffic ────▶│  Production Slot (Green) ◄──── now serving v1.3
                    │  Staging Slot (Blue)      ◄──── previous v1.2 (rollback target)
                    └─────────────────────────────┘
```

### 5.2 Prerequisites

- **App Service Plan SKU S1 or higher** — deployment slots are not available on
  Free/Shared/Basic tiers.
- Upgrade if currently on B1:

```bash
az appservice plan update \
  --name asp-ectsystem-prod \
  --resource-group rg-ectsystem-prod \
  --sku S1
```

### 5.3 Create Staging Slots

```bash
# API staging slot
az webapp deployment slot create \
  --name app-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod \
  --slot staging

# Web staging slot
az webapp deployment slot create \
  --name app-ectsystem-web-prod \
  --resource-group rg-ectsystem-prod \
  --slot staging
```

### 5.4 Slot-Specific Configuration

Mark settings that should **not** swap (e.g., connection strings pointing to
staging databases, feature flags):

```bash
# Mark connection string as slot-specific (sticky to the slot)
az webapp config connection-string set \
  --name app-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod \
  --slot staging \
  --connection-string-type SQLAzure \
  --settings \
    DefaultConnection="\
Server=sql-ect-dev-cus.database.windows.net;\
Database=ECT_Staging;\
Authentication=Active Directory Default;"

az webapp config appsettings set \
  --name app-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod \
  --slot staging \
  --slot-settings "ASPNETCORE_ENVIRONMENT=Staging"
```

> **Slot settings (sticky)** remain with the slot during a swap. **Non-sticky
> settings** travel with the app code. Use sticky settings for anything
> environment-specific (connection strings, feature flags, logging levels).

### 5.5 Deploy to Staging Slot

Update the GitHub Actions workflow to deploy to the staging slot:

```yaml
- name: Deploy to Staging Slot
  uses: azure/webapps-deploy@v3
  with:
    app-name: app-ectsystem-api-prod
    slot-name: staging
    publish-profile: ${{ secrets.AZURE_API_STAGING_PUBLISH_PROFILE }}
    package: ./publish
```

### 5.6 Validate the Staging Slot

Before swapping, verify the staging deployment:

```bash
# Staging slot URL pattern: <app-name>-<slot-name>.azurewebsites.net
curl -s -o /dev/null -w "%{http_code}" \
  https://app-ectsystem-api-prod-staging.azurewebsites.net/odata/Cases

# Run smoke tests against staging
dotnet test ECTSystem.Tests \
  --filter "Category=Smoke" \
  --environment "ApiBaseUrl=https://app-ectsystem-api-prod-staging.azurewebsites.net"
```

### 5.7 Swap Slots (Go Live)

```bash
# Swap staging → production (zero-downtime)
az webapp deployment slot swap \
  --name app-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod \
  --slot staging \
  --target-slot production

# Repeat for the Web app
az webapp deployment slot swap \
  --name app-ectsystem-web-prod \
  --resource-group rg-ectsystem-prod \
  --slot staging \
  --target-slot production
```

### 5.8 Rollback

If issues are detected after swap, immediately swap back:

```bash
az webapp deployment slot swap \
  --name app-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod \
  --slot staging \
  --target-slot production
```

> The previous production code is still running in the staging slot, so rollback
> is instantaneous.

### 5.9 Auto-Swap (Optional)

Enable auto-swap to automatically promote staging to production after
deployment:

```bash
az webapp deployment slot auto-swap \
  --name app-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod \
  --slot staging
```

> **Caution:** Only enable auto-swap if you have robust automated testing in
> your CI/CD pipeline. For the ECTSystem LOD workflow, manual swap after
> validation is recommended.

### 5.10 Full Blue/Green CI/CD Pipeline

```yaml
name: Blue/Green Deploy API

on:
  push:
    branches: [main]
    paths:
      ["ECTSystem.Api/**", "ECTSystem.Persistence/**", "ECTSystem.Shared/**"]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"
      - run:
          dotnet publish ECTSystem.Api/ECTSystem.Api.csproj -c Release -o
          ./publish
      - uses: actions/upload-artifact@v4
        with:
          name: api-package
          path: ./publish

  deploy-staging:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - uses: actions/download-artifact@v4
        with:
          name: api-package
          path: ./publish
      - uses: azure/webapps-deploy@v3
        with:
          app-name: app-ectsystem-api-prod
          slot-name: staging
          publish-profile: ${{ secrets.AZURE_API_STAGING_PUBLISH_PROFILE }}
          package: ./publish

  validate-staging:
    needs: deploy-staging
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"
      - name: Health Check
        run: |
          for i in {1..10}; do
            STATUS=$(curl -s -o /dev/null -w "%{http_code}" https://app-ectsystem-api-prod-staging.azurewebsites.net/odata/Cases)
            if [ "$STATUS" -eq 200 ]; then exit 0; fi
            sleep 5
          done
          exit 1
      - name: Run Smoke Tests
        run: dotnet test ECTSystem.Tests --filter "Category=Smoke"
        env:
          ApiBaseUrl: https://app-ectsystem-api-prod-staging.azurewebsites.net

  swap-to-production:
    needs: validate-staging
    runs-on: ubuntu-latest
    environment: production # Requires manual approval in GitHub
    steps:
      - uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}
      - name: Swap Staging to Production
        run: |
          az webapp deployment slot swap \
            --name app-ectsystem-api-prod \
            --resource-group rg-ectsystem-prod \
            --slot staging \
            --target-slot production
```

### 5.11 Blue/Green Best Practices for ECTSystem

| Practice | Guidance |
| --- | --- |
| **DB migrations** | Run before swap. Use backward-compatible changes only. |
| **Warm-up** | Configure App Init; cold starts take 10–15s. |
| **Sticky settings** | Conn strings, `ASPNETCORE_ENVIRONMENT`, flags. |
| **Health checks** | Use `/health` endpoint to detect issues post-swap. |
| **Testing the swap** | Use `--action preview` for multi-phase validation. |
| **Monitoring** | App Insights on both slots; compare error rates. |

---

## Cost Estimate

| Resource | SKU | Monthly Cost (approx.) |
| --- | --- | --- |
| App Service Plan | S1 (with slots) | ~$73 |
| App Service Plan | B1 (no slots) | ~$13 |
| Azure SQL | Basic (5 DTU) | ~$5 |
| Azure SQL | S0 (10 DTU) | ~$15 |
| Custom Domain + TLS | App Service Managed Certificate | Free |
| **Total (S1 + S0)** | — | **~$88/month** |

---

## Checklist

- [ ] Resource group created
- [ ] App Service Plan provisioned (S1+ for Blue/Green)
- [ ] API Web App created
- [ ] Web Web App created
- [ ] Managed Identity assigned to API
- [ ] SQL access granted to Managed Identity
- [ ] App settings / connection strings configured
- [ ] CORS configured
- [ ] Staging deployment slots created
- [ ] GitHub Actions workflows created
- [ ] HTTPS-only enabled
- [ ] EF Core migrations applied
- [ ] Custom domains configured (if applicable)
- [ ] Health check endpoint configured
- [ ] Application Insights enabled
- [ ] Blue/Green swap validated

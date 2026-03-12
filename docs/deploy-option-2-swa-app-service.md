# Option 2 — Azure Static Web Apps + App Service Deployment Plan

## Architecture

```text
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│  ┌───────────────────────┐         ┌──────────────────────┐     │
│  │  Azure Static Web App │         │   Azure App Service   │     │
│  │  ECTSystem.Web         │────────▶│   ECTSystem.Api       │     │
│  │  (Global CDN)          │  HTTPS  │   OData / Identity    │     │
│  │  Blazor WASM            │         └──────────┬───────────┘     │
│  └───────────────────────┘                     │               │
│                                                ▼               │
│                                     ┌──────────────────────┐   │
│                                     │    Azure SQL          │   │
│                                     │    (Entra Auth)       │   │
│                                     └──────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Prerequisites

- Azure subscription with Contributor access
- Azure CLI (`az`) and SWA CLI (`swa`) installed
- .NET 10 SDK installed
- GitHub repo connected
- Azure SQL Database already provisioned

---

## Phase 1 — Provision Azure Resources

### 1.1 Create Resource Group

```bash
az group create \
  --name rg-ectsystem-prod \
  --location centralus
```

### 1.2 Create App Service Plan + Web App (API)

```bash
az appservice plan create \
  --name asp-ectsystem-prod \
  --resource-group rg-ectsystem-prod \
  --sku B1 \
  --is-linux

az webapp create \
  --name app-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod \
  --plan asp-ectsystem-prod \
  --runtime "DOTNETCORE:10.0"
```

### 1.3 Create Azure Static Web App

```bash
az staticwebapp create \
  --name swa-ectsystem-web-prod \
  --resource-group rg-ectsystem-prod \
  --source https://github.com/donellmccoy/FormValidationExperiments \
  --branch main \
  --app-location "/ECTSystem.Web" \
  --output-location "wwwroot" \
  --login-with-github
```

> **Free tier** is sufficient for most workloads. Upgrade to Standard for custom
> auth, SLA, and more bandwidth.

### 1.4 Configure Managed Identity for API

```bash
az webapp identity assign \
  --name app-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod
```

---

## Phase 2 — Configure API Backend for SWA

### 2.1 Link SWA to API Backend

```bash
BACKEND_ID="/subscriptions/<SUB_ID>"
BACKEND_ID+="/resourceGroups/rg-ectsystem-prod"
BACKEND_ID+="/providers/Microsoft.Web/sites"
BACKEND_ID+="/app-ectsystem-api-prod"

az staticwebapp backends link \
  --name swa-ectsystem-web-prod \
  --resource-group rg-ectsystem-prod \
  --backend-resource-id "$BACKEND_ID" \
  --backend-region centralus
```

> This creates a `/api` proxy from the SWA to the App Service, eliminating CORS
> issues.

### 2.2 Alternative: Direct API Calls with CORS

If not using the linked backend approach, configure CORS on the API:

```bash
az webapp cors add \
  --name app-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod \
  --allowed-origins "https://swa-ectsystem-web-prod.azurestaticapps.net"
```

### 2.3 API App Settings

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

---

## Phase 3 — SWA Routing & Configuration

### 3.1 Create `staticwebapp.config.json`

Create `ECTSystem.Web/wwwroot/staticwebapp.config.json`:

```json
{
  "navigationFallback": {
    "rewrite": "/index.html",
    "exclude": [
      "/_framework/*",
      "/css/*",
      "/js/*",
      "/images/*",
      "*.{css,js,png,jpg,gif,ico,woff,woff2,svg,json,dll,wasm}"
    ]
  },
  "globalHeaders": {
    "X-Content-Type-Options": "nosniff",
    "X-Frame-Options": "DENY",
    "Referrer-Policy": "strict-origin-when-cross-origin"
  },
  "mimeTypes": {
    ".dll": "application/octet-stream",
    ".wasm": "application/wasm",
    ".blat": "application/octet-stream",
    ".dat": "application/octet-stream"
  }
}
```

> **Critical:** The `navigationFallback` ensures Blazor client-side routing
> works correctly. Without it, deep links return 404.

---

## Phase 4 — CI/CD with GitHub Actions

### 4.1 SWA Deployment Workflow

Azure Static Web Apps auto-generates a GitHub Actions workflow when linked.
Customize it:

```yaml
name: Deploy Blazor WASM to Azure Static Web Apps

on:
  push:
    branches: [main]
    paths:
      - "ECTSystem.Web/**"
      - "ECTSystem.Shared/**"

jobs:
  build_and_deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: Publish Blazor WASM
        run:
          dotnet publish ECTSystem.Web/ECTSystem.Web.csproj -c Release -o
          ./publish

      - name: Deploy to SWA
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.SWA_DEPLOYMENT_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: "upload"
          app_location: "./publish/wwwroot"
          skip_app_build: true
```

### 4.2 API Deployment Workflow

Same as Option 1 — deploy via `azure/webapps-deploy@v3` action.

---

## Phase 5 — Post-Deployment

### 5.1 Custom Domains

```bash
# SWA custom domain
az staticwebapp hostname set \
  --name swa-ectsystem-web-prod \
  --resource-group rg-ectsystem-prod \
  --hostname ectsystem.mil

# API custom domain (same as Option 1)
az webapp config hostname add \
  --webapp-name app-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod \
  --hostname api.ectsystem.mil
```

> SWA provides **free managed TLS certificates** for custom domains
> automatically.

### 5.2 Environment-Specific API URL

Configure the Blazor WASM app to point at the correct API URL per environment
using `appsettings.json` in `wwwroot/`:

```json
{
  "ApiBaseUrl": "https://app-ectsystem-api-prod.azurewebsites.net"
}
```

---

## Advantages over Option 1

| Factor | SWA + App Service | App Service Only |
| --- | --- | --- |
| Frontend hosting cost | Free tier available | Requires paid plan |
| Global CDN | Built-in, automatic | Requires separate Azure CDN |
| TLS certificates | Free, auto-managed | Free (App Service Managed) |
| SPA routing | Built-in `navigationFallback` | Requires custom config |
| Preview environments | Auto-created per PR | Manual staging slots |
| Frontend scaling | Unlimited (static hosting) | Tied to App Service Plan |

---

## Cost Estimate

| Resource | SKU | Monthly Cost (approx.) |
| --- | --- | --- |
| Static Web App | Free | $0 |
| Static Web App | Standard | ~$9 |
| App Service Plan (API) | B1 | ~$13 |
| Azure SQL | S0 (10 DTU) | ~$15 |
| **Total (Free SWA + B1)** | — | **~$28/month** |

---

## Checklist

- [ ] Resource group created
- [ ] App Service Plan + API Web App provisioned
- [ ] Static Web App created and linked to GitHub
- [ ] Managed Identity assigned to API
- [ ] SQL access granted
- [ ] `staticwebapp.config.json` added to `wwwroot/`
- [ ] API backend linked to SWA (or CORS configured)
- [ ] GitHub Actions workflows deployed
- [ ] Custom domains configured
- [ ] API `appsettings` URL configured in Blazor WASM
- [ ] HTTPS-only enforced on API
- [ ] EF Core migrations applied

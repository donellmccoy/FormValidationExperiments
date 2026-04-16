# Option 1a — Azure App Service (Dev / Demo Environment)

A lightweight, low-cost deployment for development and demo purposes. No staging
slots, no Blue/Green, no CI/CD pipelines — just get the app running in Azure
quickly.

## Architecture

```text
┌──────────────────────────────────────────────────────┐
│              Azure App Service (B1 Linux)             │
│                                                      │
│  ┌─────────────────┐       ┌──────────────────────┐  │
│  │  ECTSystem.Web   │       │   ECTSystem.Api       │  │
│  │  (Free F1)       │──────▶│   (Basic B1)          │  │
│  │  Blazor WASM     │ HTTPS │   OData / Identity    │  │
│  └─────────────────┘       └──────────┬───────────┘  │
│                                       │              │
│                                       ▼              │
│                            ┌──────────────────────┐  │
│                            │    Azure SQL          │  │
│                            │    sql-ect-dev-cus    │  │
│                            └──────────────────────┘  │
└──────────────────────────────────────────────────────┘
```

---

## Prerequisites

- Azure subscription
- Azure CLI (`az`) installed and logged in (`az login`)
- .NET 10 SDK installed
- Existing Azure SQL Database (`sql-ect-dev-cus.database.windows.net`, database
  `ECT`)

---

## Step 1 — Create App Service Plan

Use the existing `rg-ectsystem-dev` resource group and a **Basic B1** plan
(cheapest tier that runs 24/7):

```bash
az appservice plan create \
  --name asp-ectsystem-dev \
  --resource-group rg-ectsystem-dev \
  --location centralus \
  --sku B1 \
  --is-linux
```

> **Cost:** ~$13/month. Alternatively, use `F1` (Free) but it has 60 min/day CPU
> limits and no always-on.

---

## Step 2 — Create and Deploy the API

### 2.1 Create the API Web App

```bash
az webapp create \
  --name app-ectsystem-api-dev \
  --resource-group rg-ectsystem-dev \
  --plan asp-ectsystem-dev \
  --runtime "DOTNETCORE:10.0"
```

### 2.2 Enable Managed Identity

```bash
az webapp identity assign \
  --name app-ectsystem-api-dev \
  --resource-group rg-ectsystem-dev
```

### 2.3 Grant SQL Access

Run in Azure SQL (connect via SSMS, Azure Data Studio, or the portal query
editor):

```sql
CREATE USER [app-ectsystem-api-dev] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [app-ectsystem-api-dev];
ALTER ROLE db_datawriter ADD MEMBER [app-ectsystem-api-dev];
```

### 2.4 Configure App Settings

```bash
az webapp config appsettings set \
  --name app-ectsystem-api-dev \
  --resource-group rg-ectsystem-dev \
  --settings \
    ASPNETCORE_ENVIRONMENT="Development" \
    ConnectionStrings__DefaultConnection="\
Server=sql-ect-dev-cus.database.windows.net;\
Database=ECT;\
Authentication=Active Directory Default;"
```

### 2.5 Configure CORS

```bash
az webapp cors add \
  --name app-ectsystem-api-dev \
  --resource-group rg-ectsystem-dev \
  --allowed-origins "https://app-ectsystem-web-dev.azurewebsites.net"
```

### 2.6 Publish and Deploy

```bash
dotnet publish ECTSystem.Api/ECTSystem.Api.csproj -c Release -o ./publish-api

cd publish-api
zip -r ../ectsystem-api.zip .
cd ..

az webapp deploy \
  --name app-ectsystem-api-dev \
  --resource-group rg-ectsystem-dev \
  --src-path ectsystem-api.zip \
  --type zip
```

---

## Step 3 — Create and Deploy the Web Frontend

### 3.1 Create the Web App

```bash
az webapp create \
  --name app-ectsystem-web-dev \
  --resource-group rg-ectsystem-dev \
  --plan asp-ectsystem-dev \
  --runtime "DOTNETCORE:10.0"
```

### 3.2 Update API Base URL in the Blazor Client

Before publishing, ensure the Blazor WASM app points to the deployed API URL.
Update the API base address in `Program.cs` (or `appsettings.json` /
`wwwroot/appsettings.json`) to:

```text
https://app-ectsystem-api-dev.azurewebsites.net
```

### 3.3 Publish and Deploy

```bash
dotnet publish ECTSystem.Web/ECTSystem.Web.csproj -c Release -o ./publish-web

cd publish-web
zip -r ../ectsystem-web.zip .
cd ..

az webapp deploy \
  --name app-ectsystem-web-dev \
  --resource-group rg-ectsystem-dev \
  --src-path ectsystem-web.zip \
  --type zip
```

### 3.4 SPA Fallback Routing

Configure the startup command so Blazor client-side routing works:

```bash
az webapp config set \
  --name app-ectsystem-web-dev \
  --resource-group rg-ectsystem-dev \
  --startup-file "dotnet ECTSystem.Web.dll"
```

---

## Step 4 — Apply EF Core Migrations

Run from your local machine, targeting the Azure SQL dev database:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"

dotnet ef database update `
  --project ECTSystem.Persistence `
  --startup-project ECTSystem.Api
```

> This uses the connection string in `appsettings.Development.json` which points
> to `sql-ect-dev-cus.database.windows.net`.

---

## Step 5 — Verify

1. Open **API:** `https://app-ectsystem-api-dev.azurewebsites.net/odata/Cases`
2. Open **Web:** `https://app-ectsystem-web-dev.azurewebsites.net`

---

## Quick Redeploy After Code Changes

No CI/CD pipeline needed — just publish and zip deploy from your terminal:

```powershell
# API
dotnet publish ECTSystem.Api/ECTSystem.Api.csproj -c Release -o ./publish-api
Compress-Archive `
  -Path ./publish-api/* `
  -DestinationPath ./ectsystem-api.zip -Force
az webapp deploy `
  --name app-ectsystem-api-dev `
  --resource-group rg-ectsystem-dev `
  --src-path ectsystem-api.zip --type zip

# Web
dotnet publish ECTSystem.Web/ECTSystem.Web.csproj `
  -c Release -o ./publish-web
Compress-Archive `
  -Path ./publish-web/* `
  -DestinationPath ./ectsystem-web.zip -Force
az webapp deploy `
  --name app-ectsystem-web-dev `
  --resource-group rg-ectsystem-dev `
  --src-path ectsystem-web.zip --type zip
```

---

## Tear Down When Done

Remove everything when the demo is over:

```bash
az webapp delete --name app-ectsystem-api-dev --resource-group rg-ectsystem-dev
az webapp delete --name app-ectsystem-web-dev --resource-group rg-ectsystem-dev
az appservice plan delete \
  --name asp-ectsystem-dev \
  --resource-group rg-ectsystem-dev \
  --yes
```

> The Azure SQL database and resource group are preserved — only the App Service
> resources are removed.

---

## Cost Estimate (Dev / Demo)

| Resource | SKU | Monthly Cost |
| --- | --- | --- |
| App Service Plan | B1 (shared by both apps) | ~$13 |
| Azure SQL | S0 (existing) | ~$15 |
| **Total** | — | **~$28/month** |

> Shut down the App Service Plan when not demoing to pay $0 for compute. The SQL
> database cost remains.

---

## Comparison to Option 1 (Full)

| Feature | Option 1a (Dev/Demo) | Option 1 (Production) |
| --- | --- | --- |
| App Service SKU | B1 (~$13/mo) | S1+ (~$73/mo) |
| Deployment Slots | No | Yes (Blue/Green) |
| CI/CD Pipeline | No (manual zip deploy) | GitHub Actions |
| Custom Domain | No | Yes |
| HTTPS Cert | Default (`*.azurewebsites.net`) | Managed or App Service cert |
| Autoscaling | No | Optional |
| Monitoring | Basic (portal metrics) | Application Insights |
| Estimated Cost | ~$28/month | ~$88+/month |

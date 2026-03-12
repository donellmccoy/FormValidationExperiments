# Option 3 — Azure Container Apps Deployment Plan

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│               Azure Container Apps Environment                │
│                                                              │
│  ┌─────────────────────┐       ┌──────────────────────┐      │
│  │  ECTSystem.Web       │       │   ECTSystem.Api       │      │
│  │  (Container App)     │──────▶│   (Container App)     │      │
│  │  Blazor WASM          │ HTTPS │   OData / Identity    │      │
│  │  nginx static host    │       └──────────┬───────────┘      │
│  └─────────────────────┘                   │                │
│                                            ▼                │
│         ┌──────────────┐        ┌──────────────────────┐    │
│         │  Azure        │        │    Azure SQL          │    │
│         │  Container    │        │    (Entra Auth)       │    │
│         │  Registry     │        └──────────────────────┘    │
│         └──────────────┘                                    │
└──────────────────────────────────────────────────────────────┘
```

---

## Prerequisites

- Azure subscription with Contributor access
- Azure CLI (`az`) with `containerapp` extension
- Docker Desktop installed locally
- .NET 10 SDK installed
- GitHub repo connected
- Azure SQL Database already provisioned

---

## Phase 1 — Create Dockerfiles

### 1.1 API Dockerfile

Create `ECTSystem.Api/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ECTSystem.Api/ECTSystem.Api.csproj", "ECTSystem.Api/"]
COPY ["ECTSystem.Persistence/ECTSystem.Persistence.csproj", "ECTSystem.Persistence/"]
COPY ["ECTSystem.Shared/ECTSystem.Shared.csproj", "ECTSystem.Shared/"]
RUN dotnet restore "ECTSystem.Api/ECTSystem.Api.csproj"
COPY . .
WORKDIR "/src/ECTSystem.Api"
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Copy PDF template
COPY --from=build /app/publish/Templates/ ./Templates/

ENTRYPOINT ["dotnet", "ECTSystem.Api.dll"]
```

### 1.2 Web Dockerfile (nginx for static Blazor WASM)

Create `ECTSystem.Web/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ECTSystem.Web/ECTSystem.Web.csproj", "ECTSystem.Web/"]
COPY ["ECTSystem.Shared/ECTSystem.Shared.csproj", "ECTSystem.Shared/"]
RUN dotnet restore "ECTSystem.Web/ECTSystem.Web.csproj"
COPY . .
WORKDIR "/src/ECTSystem.Web"
RUN dotnet publish -c Release -o /app/publish

FROM nginx:alpine AS final
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY ECTSystem.Web/nginx.conf /etc/nginx/nginx.conf
EXPOSE 80
```

### 1.3 nginx Configuration for Blazor WASM

Create `ECTSystem.Web/nginx.conf`:

```nginx
events { }
http {
    include mime.types;
    types {
        application/wasm wasm;
        application/octet-stream dll blat dat;
    }

    server {
        listen 80;
        root /usr/share/nginx/html;
        index index.html;

        location / {
            try_files $uri $uri/ /index.html;
        }

        # Cache static assets aggressively
        location /_framework/ {
            add_header Cache-Control "public, max-age=31536000, immutable";
        }
    }
}
```

---

## Phase 2 — Provision Azure Resources

### 2.1 Create Azure Container Registry

```bash
az acr create \
  --name acrectsystem \
  --resource-group rg-ectsystem-prod \
  --sku Basic \
  --admin-enabled true
```

### 2.2 Create Container Apps Environment

```bash
az containerapp env create \
  --name cae-ectsystem-prod \
  --resource-group rg-ectsystem-prod \
  --location centralus
```

### 2.3 Build and Push Images

```bash
# Login to ACR
az acr login --name acrectsystem

# Build and push API image
docker build -t acrectsystem.azurecr.io/ectsystem-api:latest -f ECTSystem.Api/Dockerfile .
docker push acrectsystem.azurecr.io/ectsystem-api:latest

# Build and push Web image
docker build -t acrectsystem.azurecr.io/ectsystem-web:latest -f ECTSystem.Web/Dockerfile .
docker push acrectsystem.azurecr.io/ectsystem-web:latest
```

> **Alternative:** Use `az acr build` to build directly in ACR (no local Docker needed):
> ```bash
> az acr build --registry acrectsystem --image ectsystem-api:latest -f ECTSystem.Api/Dockerfile .
> ```

---

## Phase 3 — Deploy Container Apps

### 3.1 Deploy API Container App

```bash
az containerapp create \
  --name ca-ectsystem-api \
  --resource-group rg-ectsystem-prod \
  --environment cae-ectsystem-prod \
  --image acrectsystem.azurecr.io/ectsystem-api:latest \
  --registry-server acrectsystem.azurecr.io \
  --registry-username acrectsystem \
  --registry-password <ACR_PASSWORD> \
  --target-port 8080 \
  --ingress external \
  --min-replicas 1 \
  --max-replicas 5 \
  --cpu 0.5 \
  --memory 1.0Gi \
  --env-vars \
    "ConnectionStrings__DefaultConnection=Server=sql-ect-dev-cus.database.windows.net;Database=ECT;Authentication=Active Directory Default;" \
    "ASPNETCORE_ENVIRONMENT=Production"
```

### 3.2 Deploy Web Container App

```bash
az containerapp create \
  --name ca-ectsystem-web \
  --resource-group rg-ectsystem-prod \
  --environment cae-ectsystem-prod \
  --image acrectsystem.azurecr.io/ectsystem-web:latest \
  --registry-server acrectsystem.azurecr.io \
  --registry-username acrectsystem \
  --registry-password <ACR_PASSWORD> \
  --target-port 80 \
  --ingress external \
  --min-replicas 0 \
  --max-replicas 3 \
  --cpu 0.25 \
  --memory 0.5Gi
```

> **Scale to zero:** Set `--min-replicas 0` on the Web container since it serves static files and can cold-start quickly. Keep `--min-replicas 1` on the API to avoid EF Core cold-start latency.

### 3.3 Enable Managed Identity

```bash
az containerapp identity assign \
  --name ca-ectsystem-api \
  --resource-group rg-ectsystem-prod \
  --system-assigned
```

Grant SQL access (same as Option 1, Phase 1.6).

---

## Phase 4 — CI/CD with GitHub Actions

### 4.1 API Deployment Workflow

```yaml
name: Deploy API to Container Apps

on:
  push:
    branches: [main]
    paths: ['ECTSystem.Api/**', 'ECTSystem.Persistence/**', 'ECTSystem.Shared/**']

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Build and push to ACR
        run: |
          az acr build \
            --registry acrectsystem \
            --image ectsystem-api:${{ github.sha }} \
            -f ECTSystem.Api/Dockerfile .

      - name: Deploy to Container App
        run: |
          az containerapp update \
            --name ca-ectsystem-api \
            --resource-group rg-ectsystem-prod \
            --image acrectsystem.azurecr.io/ectsystem-api:${{ github.sha }}
```

---

## Phase 5 — Revision-Based Blue/Green Deployments

Container Apps supports traffic splitting across revisions:

```bash
# Deploy new revision with 0% traffic
az containerapp update \
  --name ca-ectsystem-api \
  --resource-group rg-ectsystem-prod \
  --image acrectsystem.azurecr.io/ectsystem-api:v2 \
  --revision-suffix v2

# Split traffic: 90% old, 10% new (canary)
az containerapp ingress traffic set \
  --name ca-ectsystem-api \
  --resource-group rg-ectsystem-prod \
  --revision-weight ca-ectsystem-api--v1=90 ca-ectsystem-api--v2=10

# Full cutover
az containerapp ingress traffic set \
  --name ca-ectsystem-api \
  --resource-group rg-ectsystem-prod \
  --revision-weight ca-ectsystem-api--v2=100
```

---

## Phase 6 — Scaling Configuration

### 6.1 HTTP-Based Autoscaling

```bash
az containerapp update \
  --name ca-ectsystem-api \
  --resource-group rg-ectsystem-prod \
  --scale-rule-name http-rule \
  --scale-rule-type http \
  --scale-rule-http-concurrency 50 \
  --min-replicas 1 \
  --max-replicas 10
```

---

## Cost Estimate

| Resource | Configuration | Monthly Cost (approx.) |
|----------|--------------|----------------------|
| Container Apps (API) | 0.5 vCPU / 1Gi, 1 replica | ~$36 |
| Container Apps (Web) | 0.25 vCPU / 0.5Gi, scale-to-zero | ~$0–18 |
| Container Registry | Basic | ~$5 |
| Azure SQL | S0 (10 DTU) | ~$15 |
| **Total** | | **~$56–74/month** |

---

## Checklist

- [ ] Dockerfiles created for API and Web
- [ ] nginx.conf created for Blazor WASM SPA routing
- [ ] Azure Container Registry provisioned
- [ ] Container Apps Environment created
- [ ] Images built and pushed
- [ ] API Container App deployed with ingress
- [ ] Web Container App deployed with ingress
- [ ] Managed Identity assigned and SQL access granted
- [ ] GitHub Actions CI/CD configured
- [ ] Scaling rules configured
- [ ] CORS configured on API (if separate origins)
- [ ] Custom domains configured
- [ ] EF Core migrations applied

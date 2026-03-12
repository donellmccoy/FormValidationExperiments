# Azure Deployment Options — ECTSystem

## Solution Overview

| Component | Technology | Notes |
|-----------|-----------|-------|
| **ECTSystem.Api** | ASP.NET Core OData API (.NET 10) | EF Core, Identity, PDFsharp |
| **ECTSystem.Web** | Blazor WebAssembly (standalone) | Radzen, OData client, Service Worker/PWA |
| **Database** | Azure SQL | `sql-ect-dev-cus.database.windows.net`, Entra-only auth |

---

## Option 1 — Azure App Service

Deploy **ECTSystem.Api** to a Web App (Linux or Windows App Service Plan). Deploy **ECTSystem.Web** as a second Web App, or serve it from the API project using `UseBlazorFrameworkFiles()`.

- Straightforward CI/CD via GitHub Actions or Azure DevOps.
- Easy scaling (scale up/out), deployment slots, and managed TLS.
- **Best for:** most teams, simplest path to production.

> **See:** [docs/deploy-option-1-app-service.md](deploy-option-1-app-service.md)

---

## Option 2 — Azure Static Web Apps + App Service

Deploy **ECTSystem.Web** (Blazor WASM) to **Azure Static Web Apps** — globally distributed, free tier available, no app service plan needed for the frontend. Deploy **ECTSystem.Api** to a separate **App Service**.

- **Best for:** cost optimization on the frontend, global CDN distribution.

> **See:** [docs/deploy-option-2-swa-app-service.md](deploy-option-2-swa-app-service.md)

---

## Option 3 — Azure Container Apps

Containerize both projects (Dockerfile per project), deploy to **Azure Container Apps**.

- Built-in autoscaling (including scale to zero), Dapr support, managed ingress.
- **Best for:** teams already using containers, microservice-oriented architectures, or wanting scale-to-zero.

> **See:** [docs/deploy-option-3-container-apps.md](deploy-option-3-container-apps.md)

---

## Option 4 — Azure Kubernetes Service (AKS)

Full Kubernetes orchestration for both services.

- **Best for:** large-scale, multi-service deployments where you need fine-grained control over networking, scaling, and service mesh.
- Highest operational overhead.

> **See:** [docs/deploy-option-4-aks.md](deploy-option-4-aks.md)

---

## Option 5 — Azure Functions (API Only)

Refactor the API into Azure Functions (HTTP triggers per endpoint). Not a natural fit here since the project uses OData + EF Core + Identity with middleware — significant refactoring required.

- **Best for:** event-driven or serverless workloads.

> **See:** [docs/deploy-option-5-azure-functions.md](deploy-option-5-azure-functions.md)

---

## Cross-Cutting Considerations

| Factor | Notes |
|--------|-------|
| **Azure SQL** | Already provisioned (`sql-ect-dev-cus.database.windows.net`). All options connect via `Authentication=Active Directory Default`. |
| **Identity/Auth** | ASP.NET Identity with JWT — works out of the box on App Service and Container Apps. |
| **Service Worker/PWA** | Blazor WASM includes a service worker — Static Web Apps and App Service both support this. |
| **PDF Generation** | PDFsharp runs fine on App Service (Windows) and containers. On Linux App Service, verify font availability. |
| **CORS** | Already configured (`BlazorClient` policy). Needed when API and Web are on separate origins. |

## Recommendation

**Azure App Service** for the API + **Azure Static Web Apps** for the Blazor WASM frontend. This gives the lowest operational complexity, free/cheap frontend hosting with global CDN, and a natural separation of concerns. Azure SQL and Entra auth are already in place.

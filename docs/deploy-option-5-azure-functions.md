# Option 5 — Azure Functions + Static Web Apps Deployment Plan

## Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                                                                  │
│  ┌───────────────────────┐         ┌──────────────────────────┐  │
│  │  Azure Static Web App │         │   Azure Functions         │  │
│  │  ECTSystem.Web         │────────▶│   (Consumption/Flex)      │  │
│  │  (Global CDN)          │  HTTPS  │   OData / Identity        │  │
│  │  Blazor WASM            │         │   Serverless API          │  │
│  └───────────────────────┘         └──────────┬───────────────┘  │
│                                               │                 │
│                                               ▼                 │
│                                    ┌──────────────────────┐     │
│                                    │    Azure SQL          │     │
│                                    │    (Entra Auth)       │     │
│                                    └──────────────────────┘     │
└──────────────────────────────────────────────────────────────────┘
```

---

## Prerequisites

- Azure subscription with Contributor access
- Azure CLI (`az`) with `functionapp` extension
- Azure Functions Core Tools (`func`) v4+
- .NET 10 SDK installed
- GitHub repo connected
- Azure SQL Database already provisioned

---

## Important Considerations

> **This option requires significant refactoring.** The current `ECTSystem.Api` is an ASP.NET Core OData API with middleware, Identity endpoints, and EF Core — not a Functions project. This plan covers the migration path.

### What Changes

| Component | Current | After Migration |
|-----------|---------|----------------|
| API Framework | ASP.NET Core OData | Azure Functions (Isolated Worker) |
| Hosting | Kestrel / IIS | Azure Functions Runtime |
| Routing | OData convention routing | Function triggers + route attributes |
| Identity | `MapIdentityApi()` endpoints | Separate Identity Function or B2C |
| Middleware | Custom middleware pipeline | Function filters / middleware |
| EF Core | Scoped `DbContext` via DI | Same (works in Functions) |
| PDF Generation | PDFsharp in-process | Same or move to Durable Functions |

---

## Phase 1 — Create Functions Project

### 1.1 Create New Functions Project

```bash
func init ECTSystem.Functions --dotnet-isolated --target-framework net10.0
cd ECTSystem.Functions
func new --name CasesFunction --template "HTTP trigger"
```

### 1.2 Project Structure

```
ECTSystem.Functions/
├── Program.cs              # Host builder with DI
├── CasesFunction.cs        # Cases CRUD endpoints
├── MembersFunction.cs      # Members endpoints
├── DocumentsFunction.cs    # Documents endpoints
├── AuthFunction.cs         # Identity/auth endpoints
└── host.json               # Functions runtime config
```

### 1.3 Host Configuration (`Program.cs`)

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Reuse existing service registration from ECTSystem.Api
        services.AddDbContextFactory<EctDbContext>(options =>
            options.UseSqlServer(
                Environment.GetEnvironmentVariable("SqlConnectionString"),
                x => x.MigrationsAssembly("ECTSystem.Persistence")));
    })
    .Build();

host.Run();
```

### 1.4 Convert Controllers to Functions

**Before (ASP.NET Core OData Controller):**
```csharp
public class CasesController : ODataController
{
    [EnableQuery]
    public IActionResult Get() => Ok(_context.LineOfDutyCases);

    public async Task<IActionResult> Post([FromBody] LineOfDutyCase lodCase) { ... }
}
```

**After (Azure Function):**
```csharp
public class CasesFunction
{
    private readonly IDbContextFactory<EctDbContext> _contextFactory;

    public CasesFunction(IDbContextFactory<EctDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    [Function("GetCases")]
    public async Task<HttpResponseData> GetCases(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "cases")] HttpRequestData req)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var cases = await context.LineOfDutyCases.ToListAsync();
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(cases);
        return response;
    }

    [Function("GetCase")]
    public async Task<HttpResponseData> GetCase(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "cases/{id:int}")] HttpRequestData req,
        int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var lodCase = await context.LineOfDutyCases.FindAsync(id);
        if (lodCase is null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(lodCase);
        return response;
    }

    [Function("CreateCase")]
    public async Task<HttpResponseData> CreateCase(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "cases")] HttpRequestData req)
    {
        var lodCase = await req.ReadFromJsonAsync<LineOfDutyCase>();
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.LineOfDutyCases.Add(lodCase!);
        await context.SaveChangesAsync();

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(lodCase);
        return response;
    }
}
```

> **OData Limitation:** Azure Functions does not natively support OData query syntax (`$select`, `$expand`, `$filter`). You'll need to either:
> 1. Implement manual query parsing
> 2. Use a library like `Microsoft.AspNetCore.OData` in the Functions isolated worker
> 3. Replace OData queries with explicit REST endpoints

---

## Phase 2 — Identity / Authentication

### Option A: Azure AD B2C (Recommended for Functions)

Replace ASP.NET Identity with Azure AD B2C:

```bash
az ad b2c create \
  --display-name ectsystem-b2c \
  --domain ectsystem.onmicrosoft.com
```

Configure Functions for B2C token validation:

```csharp
[Function("GetCases")]
public async Task<HttpResponseData> GetCases(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "cases")] HttpRequestData req,
    FunctionContext context)
{
    // Validate JWT from B2C
    var principal = context.Features.Get<ClaimsPrincipal>();
    if (principal?.Identity?.IsAuthenticated != true)
    {
        return req.CreateResponse(HttpStatusCode.Unauthorized);
    }
    // ...
}
```

### Option B: Keep ASP.NET Identity as Separate Service

Deploy Identity endpoints as a separate App Service while using Functions for the data API.

---

## Phase 3 — Provision Azure Resources

### 3.1 Create Storage Account (Required for Functions)

```bash
az storage account create \
  --name stectsystemprod \
  --resource-group rg-ectsystem-prod \
  --location centralus \
  --sku Standard_LRS
```

### 3.2 Create Function App

```bash
# Consumption plan (pay-per-execution)
az functionapp create \
  --name func-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod \
  --storage-account stectsystemprod \
  --consumption-plan-location centralus \
  --runtime dotnet-isolated \
  --runtime-version 10 \
  --functions-version 4 \
  --os-type Linux
```

### 3.3 Alternative: Flex Consumption Plan

```bash
az functionapp create \
  --name func-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod \
  --storage-account stectsystemprod \
  --flexconsumption-location centralus \
  --runtime dotnet-isolated \
  --runtime-version 10
```

> **Flex Consumption** provides faster cold starts and VNet integration, recommended for production workloads.

### 3.4 Create Static Web App

Same as [Option 2](deploy-option-2-swa-app-service.md), Phase 1.3.

### 3.5 Configure Function App Settings

```bash
az functionapp config appsettings set \
  --name func-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod \
  --settings \
    "SqlConnectionString=Server=sql-ect-dev-cus.database.windows.net;Database=ECT;Authentication=Active Directory Default;"
```

### 3.6 Enable Managed Identity

```bash
az functionapp identity assign \
  --name func-ectsystem-api-prod \
  --resource-group rg-ectsystem-prod
```

---

## Phase 4 — CI/CD with GitHub Actions

```yaml
name: Deploy Functions API

on:
  push:
    branches: [main]
    paths: ['ECTSystem.Functions/**', 'ECTSystem.Persistence/**', 'ECTSystem.Shared/**']

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Build
        run: dotnet publish ECTSystem.Functions/ECTSystem.Functions.csproj -c Release -o ./publish

      - name: Deploy to Azure Functions
        uses: azure/functions-action@v2
        with:
          app-name: func-ectsystem-api-prod
          package: ./publish
          publish-profile: ${{ secrets.AZURE_FUNCTIONS_PUBLISH_PROFILE }}
```

---

## Phase 5 — SWA Linked Backend

Link the Static Web App to the Function App to proxy `/api` requests:

```bash
az staticwebapp backends link \
  --name swa-ectsystem-web-prod \
  --resource-group rg-ectsystem-prod \
  --backend-resource-id "/subscriptions/<SUB_ID>/resourceGroups/rg-ectsystem-prod/providers/Microsoft.Web/sites/func-ectsystem-api-prod" \
  --backend-region centralus
```

---

## Migration Effort Estimate

| Task | Effort |
|------|--------|
| Create Functions project with DI | Low |
| Convert 6 controllers to Functions | Medium |
| Replace OData query support | High |
| Migrate Identity endpoints | High |
| Update Blazor WASM client HTTP calls | Medium |
| Update EF Core configuration | Low |
| Migrate custom middleware to Function filters | Low |
| Testing and validation | Medium |
| **Total estimate** | **High — significant refactoring** |

---

## Cost Estimate

| Resource | Configuration | Monthly Cost (approx.) |
|----------|--------------|----------------------|
| Functions (Consumption) | Pay-per-execution | ~$0–5 (low traffic) |
| Functions (Flex Consumption) | Per-invocation + vCPU/GB-s | ~$5–20 |
| Storage Account | Standard LRS | ~$1 |
| Static Web App | Free | $0 |
| Azure SQL | S0 (10 DTU) | ~$15 |
| **Total (Consumption + Free SWA)** | | **~$16–41/month** |

> **Lowest cost option** when traffic is low or spiky. Cost scales linearly with usage.

---

## When to Choose This Option

- Extremely low or spiky traffic patterns
- Budget is the primary constraint
- Team is willing to invest in refactoring
- OData can be replaced with explicit REST endpoints
- Identity can be externalized to Azure AD B2C

## When NOT to Choose This Option

- OData query support is critical to the Blazor WASM client
- ASP.NET Identity with custom user stores is required
- Cold starts are unacceptable for user experience
- Team prefers minimal refactoring effort

---

## Checklist

- [ ] Functions project created with isolated worker model
- [ ] Controllers converted to HTTP-triggered Functions
- [ ] OData queries replaced or adapted
- [ ] Identity solution selected (B2C or separate service)
- [ ] Storage Account created
- [ ] Function App provisioned
- [ ] Static Web App created
- [ ] Managed Identity assigned and SQL access granted
- [ ] SWA backend linked to Function App
- [ ] Blazor WASM HTTP client updated
- [ ] CI/CD pipelines configured
- [ ] EF Core migrations applied
- [ ] Cold start performance validated

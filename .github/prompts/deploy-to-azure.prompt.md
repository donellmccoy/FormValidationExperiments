---
name: "Deploy ECTSystem to Azure (API + Web)"
description: "Full end-to-end dev deployment: publish ECTSystem.Api to App Service (app-ectsystem-api-dev) and ECTSystem.Web to Azure Static Web Apps (swa-ectsystem-web-dev), then smoke-test both. Assumes resources are already provisioned per docs/deployment/deploy-option-1a-app-service-dev.md and deploy-option-2-swa-app-service.md."
---

Deploy **both** the API (ASP.NET Core OData → Azure App Service) and the Web frontend (Blazor WASM → Azure Static Web Apps) to the **dev** environment in one run.

> **Pre-reqs (one-time provisioning, done if following the linked docs):**
> - Resource group `rg-ectsystem-dev` exists
> - App Service `app-ectsystem-api-dev` exists with managed identity, SQL access, app settings, and CORS configured — see [deploy-option-1a-app-service-dev.md](../../docs/deployment/deploy-option-1a-app-service-dev.md)
> - Static Web App `swa-ectsystem-web-dev` exists — see [deploy-option-2-swa-app-service.md](../../docs/deployment/deploy-option-2-swa-app-service.md)
> - SWA CLI installed: `npm install -g @azure/static-web-apps-cli`
> - `ECTSystem.Web/wwwroot/staticwebapp.config.json` exists with the Blazor `navigationFallback` rule
> - `ECTSystem.Web/wwwroot/appsettings.json` `ApiBaseUrl` points at `https://app-ectsystem-api-dev.azurewebsites.net`

## Variables

| Name | Value |
|------|-------|
| Resource group | `rg-ectsystem-dev` |
| App Service (API) | `app-ectsystem-api-dev` |
| Static Web App (Web) | `swa-ectsystem-web-dev` |
| API project | `ECTSystem.Api/ECTSystem.Api.csproj` |
| Web project | `ECTSystem.Web/ECTSystem.Web.csproj` |
| API publish dir / zip | `./publish-api` / `./ectsystem-api.zip` |
| Web publish dir | `./publish-web` (static content under `./publish-web/wwwroot`) |
| API URL | `https://app-ectsystem-api-dev.azurewebsites.net` |

## Steps

1. **Confirm** the user wants to deploy **both** the API and Web to **dev**. Stop if not confirmed.

2. **Pre-flight checks** — verify tooling and auth:

	```powershell
	az account show --output json | ConvertFrom-Json | Select-Object name, id, tenantId
	dotnet --version
	swa --version
	Test-Path ECTSystem.Web/wwwroot/staticwebapp.config.json
	```

	Stop on any failure. (Missing SWA config means Blazor deep links will return 404 — instruct the user to add it before retrying.)

3. **Build solution once** to fail fast on compile errors:

	```powershell
	dotnet build ECTSystem.slnx -c Release /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary
	```

	Stop on non-zero exit code.

### API deployment

4. **Publish + package** the API:

	```powershell
	Remove-Item -Recurse -Force ./publish-api -ErrorAction SilentlyContinue
	Remove-Item -Force ./ectsystem-api.zip -ErrorAction SilentlyContinue
	dotnet publish ECTSystem.Api/ECTSystem.Api.csproj -c Release -o ./publish-api --no-build
	Compress-Archive -Path ./publish-api/* -DestinationPath ./ectsystem-api.zip -Force
	```

5. **Deploy** the API zip to App Service. **Always pass `--async true`** — the synchronous mode hangs indefinitely at "Warming up Kudu before deployment." on this Linux App Service (verified 2026-04-29, deployment id `456dc2b2-ce53-4af1-a3be-78a3f1ee16ca`). The async call returns a deployment id; poll until `RuntimeSuccessful`:

	```powershell
	az webapp deploy `
	  --name app-ectsystem-api-dev `
	  --resource-group rg-ectsystem-dev `
	  --src-path ./ectsystem-api.zip `
	  --type zip `
	  --async true
	```

	Typical timing: Building ~1s → Build successful ~17s → Starting site ~33–96s → `RuntimeSuccessful` ~112s. If the terminal does hang, kill it and re-run with `--async true`.

6. **Smoke-test** the API:

	```powershell
	Invoke-WebRequest -Uri https://app-ectsystem-api-dev.azurewebsites.net/odata/$metadata -UseBasicParsing |
	  Select-Object StatusCode, @{n='Length';e={$_.Content.Length}}
	```

	Expect `StatusCode = 200`. On 5xx, tail logs and stop:

	```powershell
	az webapp log tail --name app-ectsystem-api-dev --resource-group rg-ectsystem-dev
	```

	**Do not proceed to the Web deployment if the API smoke-test fails** — the Blazor app will be broken.

### Web deployment

7. **Publish** the Blazor WASM app:

	```powershell
	Remove-Item -Recurse -Force ./publish-web -ErrorAction SilentlyContinue
	dotnet publish ECTSystem.Web/ECTSystem.Web.csproj -c Release -o ./publish-web --no-build
	```

8. **Resolve the SWA deployment token** (skip if `$env:SWA_DEPLOYMENT_TOKEN` already set):

	```powershell
	$env:SWA_DEPLOYMENT_TOKEN = (
	  az staticwebapp secrets list `
	    --name swa-ectsystem-web-dev `
	    --resource-group rg-ectsystem-dev `
	    --query "properties.apiKey" -o tsv
	)
	```

9. **Deploy** the static content:

	```powershell
	swa deploy ./publish-web/wwwroot `
	  --deployment-token $env:SWA_DEPLOYMENT_TOKEN `
	  --env production
	```

10. **Smoke-test** the Web app:

	```powershell
	$webHost = (az staticwebapp show --name swa-ectsystem-web-dev --resource-group rg-ectsystem-dev --query "defaultHostname" -o tsv)
	Invoke-WebRequest -Uri "https://$webHost" -UseBasicParsing |
	  Select-Object StatusCode, @{n='Length';e={$_.Content.Length}}
	```

	Expect `StatusCode = 200` with a non-trivial body (the Blazor host page).

11. **Report** to the user:
	- API URL + smoke-test status
	- Web URL + smoke-test status
	- Any warnings from the publish or deploy steps

## Notes

- EF Core migrations are **not** run by this prompt. If schema changed, run `dotnet ef database update --project ECTSystem.Persistence --startup-project ECTSystem.Api` separately **before** step 6.
- For provisioning resources from scratch, follow [deploy-option-1a-app-service-dev.md](../../docs/deployment/deploy-option-1a-app-service-dev.md) and [deploy-option-2-swa-app-service.md](../../docs/deployment/deploy-option-2-swa-app-service.md) first.
- For a single-target deploy (API-only or Web-only), use [deploy-api-app-service.prompt.md](deploy-api-app-service.prompt.md) or [deploy-web-static-web-app.prompt.md](deploy-web-static-web-app.prompt.md).

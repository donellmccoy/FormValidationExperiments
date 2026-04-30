---
name: "Deploy Web to Azure Static Web App"
description: "Publish ECTSystem.Web (Blazor WASM) and deploy the static output to an Azure Static Web App via the SWA CLI. Assumes the SWA resource and deployment token are already provisioned per docs/deployment/deploy-option-2-swa-app-service.md."
---

Publish **ECTSystem.Web** (Blazor WebAssembly) and deploy `wwwroot` to the existing Azure Static Web App.

> **Pre-reqs (one-time):**
> - Static Web App resource exists (e.g. `swa-ectsystem-web-dev` in `rg-ectsystem-dev`)
> - SWA deployment token is available — either set as env var `$env:SWA_DEPLOYMENT_TOKEN` or retrievable via `az staticwebapp secrets list`
> - SWA CLI installed: `npm install -g @azure/static-web-apps-cli` (provides the `swa` command)
> - `ECTSystem.Web/wwwroot/staticwebapp.config.json` exists with the Blazor `navigationFallback` rule (see [deploy-option-2-swa-app-service.md](../../docs/deployment/deploy-option-2-swa-app-service.md) §3.1)
> - `ECTSystem.Web/wwwroot/appsettings.json` `ApiBaseUrl` points at the deployed API (e.g. `https://app-ectsystem-api-dev.azurewebsites.net`)

## Variables

| Name | Value |
|------|-------|
| Resource group | `rg-ectsystem-dev` |
| Static Web App | `swa-ectsystem-web-dev` |
| Project | `ECTSystem.Web/ECTSystem.Web.csproj` |
| Publish dir | `./publish-web` |
| Static content dir | `./publish-web/wwwroot` |

## Steps

1. **Confirm** the user wants to deploy to the **dev** Static Web App. Stop if not confirmed.

2. **Verify Azure CLI auth** and SWA CLI presence:

	```powershell
	az account show --output json | ConvertFrom-Json | Select-Object name, id, tenantId
	swa --version
	```

	Stop and instruct the user to fix if either fails.

3. **Sanity-check** the SWA config file exists (Blazor SPA fallback):

	```powershell
	Test-Path ECTSystem.Web/wwwroot/staticwebapp.config.json
	```

	If `False`, stop and tell the user to add it (see [deploy-option-2-swa-app-service.md](../../docs/deployment/deploy-option-2-swa-app-service.md) §3.1) — without it, deep links return 404.

4. **Clean + publish** the Blazor WASM app:

	```powershell
	Remove-Item -Recurse -Force ./publish-web -ErrorAction SilentlyContinue
	dotnet publish ECTSystem.Web/ECTSystem.Web.csproj -c Release -o ./publish-web
	```

	Stop on non-zero exit code.

5. **Resolve the deployment token** (skip if `$env:SWA_DEPLOYMENT_TOKEN` already set):

	```powershell
	$env:SWA_DEPLOYMENT_TOKEN = (
	  az staticwebapp secrets list `
	    --name swa-ectsystem-web-dev `
	    --resource-group rg-ectsystem-dev `
	    --query "properties.apiKey" -o tsv
	)
	```

6. **Deploy** the published static content to SWA:

	```powershell
	swa deploy ./publish-web/wwwroot `
	  --deployment-token $env:SWA_DEPLOYMENT_TOKEN `
	  --env production
	```

	Capture the public URL printed by the CLI (e.g. `https://swa-ectsystem-web-dev.azurestaticapps.net`).

7. **Smoke-test** the deployed site:

	```powershell
	$url = (az staticwebapp show --name swa-ectsystem-web-dev --resource-group rg-ectsystem-dev --query "defaultHostname" -o tsv)
	Invoke-WebRequest -Uri "https://$url" -UseBasicParsing |
	  Select-Object StatusCode, @{n='Length';e={$_.Content.Length}}
	```

	Expect `StatusCode = 200` with a non-trivial body containing the Blazor boot script.

8. **Report** the deployed URL and smoke-test result back to the user.

## Notes

- The API must be reachable from the browser. If using direct API calls (not the SWA linked-backend `/api` proxy), ensure CORS on `app-ectsystem-api-dev` includes the SWA hostname.
- For provisioning the SWA + linked backend from scratch, follow [deploy-option-2-swa-app-service.md](../../docs/deployment/deploy-option-2-swa-app-service.md).
- Pair this with [deploy-api-app-service.prompt.md](deploy-api-app-service.prompt.md) for a full end-to-end deploy.

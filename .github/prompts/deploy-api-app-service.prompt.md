---
name: "Deploy API to Azure App Service"
description: "Publish and zip-deploy ECTSystem.Api to the dev Azure App Service (app-ectsystem-api-dev). Assumes the App Service, managed identity, SQL user, and app settings are already provisioned per docs/deployment/deploy-option-1a-app-service-dev.md."
---

Publish **ECTSystem.Api** (ASP.NET Core OData) and deploy it to the existing dev App Service via a zip package.

> **Pre-reqs (one-time, already done if following [deploy-option-1a-app-service-dev.md](../../docs/deployment/deploy-option-1a-app-service-dev.md)):**
> - Resource group `rg-ectsystem-dev` exists
> - App Service plan `asp-ectsystem-dev` and web app `app-ectsystem-api-dev` exist (DOTNETCORE:10.0)
> - Managed identity assigned and granted `db_datareader` / `db_datawriter` on the ECT database
> - App settings (`ASPNETCORE_ENVIRONMENT`, `ConnectionStrings__DefaultConnection`) configured
> - CORS allows the Web origin

## Variables

| Name | Value |
|------|-------|
| Resource group | `rg-ectsystem-dev` |
| Web app | `app-ectsystem-api-dev` |
| Project | `ECTSystem.Api/ECTSystem.Api.csproj` |
| Publish dir | `./publish-api` |
| Zip path | `./ectsystem-api.zip` |
| Public URL | `https://app-ectsystem-api-dev.azurewebsites.net` |

## Steps

1. **Confirm** the user wants to deploy to the **dev** App Service. Stop if not confirmed.

2. **Verify Azure CLI auth** (run in a pwsh terminal):

	```powershell
	az account show --output json | ConvertFrom-Json | Select-Object name, id, tenantId
	```

	If not authenticated or wrong subscription, instruct the user to `az login` / `az account set --subscription <id>` and stop.

3. **Clean + publish** the API in Release configuration:

	```powershell
	Remove-Item -Recurse -Force ./publish-api -ErrorAction SilentlyContinue
	dotnet publish ECTSystem.Api/ECTSystem.Api.csproj -c Release -o ./publish-api
	```

	Stop on non-zero exit code.

4. **Package** the publish output as a zip:

	```powershell
	Remove-Item -Force ./ectsystem-api.zip -ErrorAction SilentlyContinue
	Compress-Archive -Path ./publish-api/* -DestinationPath ./ectsystem-api.zip -Force
	```

5. **Deploy** to App Service:

	```powershell
	az webapp deploy `
	  --name app-ectsystem-api-dev `
	  --resource-group rg-ectsystem-dev `
	  --src-path ./ectsystem-api.zip `
	  --type zip
	```

6. **Smoke-test** the deployment:

	```powershell
	Invoke-WebRequest -Uri https://app-ectsystem-api-dev.azurewebsites.net/odata/$metadata -UseBasicParsing |
	  Select-Object StatusCode, @{n='Length';e={$_.Content.Length}}
	```

	Expect `StatusCode = 200` and a non-trivial `Length`. If 5xx, tail logs:

	```powershell
	az webapp log tail --name app-ectsystem-api-dev --resource-group rg-ectsystem-dev
	```

7. **Report** the deployed URL and smoke-test result back to the user.

## Notes

- EF Core migrations are **not** applied by this prompt. Run them separately with `dotnet ef database update --project ECTSystem.Persistence --startup-project ECTSystem.Api` if schema changes are part of the deploy.
- For provisioning new resources from scratch, use [deploy-option-1a-app-service-dev.md](../../docs/deployment/deploy-option-1a-app-service-dev.md) instead.

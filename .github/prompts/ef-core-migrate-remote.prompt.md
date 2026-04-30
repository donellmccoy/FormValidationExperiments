---
name: "EF Core Migrate (REMOTE)"
description: "Apply EF Core migrations to the REMOTE Azure SQL ECT database for both EctDbContext (operational) and EctIdentityDbContext (identity). DESTRUCTIVE — requires explicit confirmation."
---

Apply pending EF Core migrations to the **REMOTE** Azure SQL database (`sql-ect-dev-cus.database.windows.net/ECT`) for **both** DbContexts. The Persistence layer hosts two contexts with separate migrations folders, so each `dotnet ef` command MUST specify `--context`.

> **WARNING**: This targets the shared dev Azure SQL database. Schema changes are effectively irreversible. **Require explicit user confirmation (e.g. "yes" / "GO") before invoking step 5 (apply) below.**

## Variables

| Name | Value |
|------|-------|
| Migrations project | `ECTSystem.Persistence` |
| Startup project | `ECTSystem.Api` |
| Operational context | `EctDbContext` (migrations dir: `Migrations/`) |
| Identity context | `EctIdentityDbContext` (migrations dir: `Migrations/Identity/`) |
| Connection-string key | `ConnectionStrings:EctDatabase` |
| REMOTE connection (Production) | `Server=tcp:sql-ect-dev-cus.database.windows.net,1433;Initial Catalog=ECT;Encrypt=True;Authentication="Active Directory Default";` |
| Remote server | `sql-ect-dev-cus.database.windows.net` |
| Remote database | `ECT` |
| Resource group | `rg-ectsystem-dev` |
| Environment | `Production` |

The `dotnet ef` CLI reads the connection string from the startup project's `appsettings.Production.json` when `ASPNETCORE_ENVIRONMENT=Production`.

## Steps

### 1. Pre-flight — verify tooling and Entra auth

```powershell
dotnet tool list -g | Select-String dotnet-ef
# If missing:
# dotnet tool install --global dotnet-ef
# If outdated:
# dotnet tool update --global dotnet-ef

# Confirm az CLI is signed in to the correct tenant — Authentication="Active Directory Default"
# uses this token chain.
az account show --output json | ConvertFrom-Json | Select-Object name, id, tenantId
```

### 2. Target REMOTE environment

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
```

### 3. List pending migrations for both contexts (read-only, safe)

```powershell
dotnet ef migrations list `
  --project ECTSystem.Persistence `
  --startup-project ECTSystem.Api `
  --context EctDbContext

dotnet ef migrations list `
  --project ECTSystem.Persistence `
  --startup-project ECTSystem.Api `
  --context EctIdentityDbContext
```

### 4. (Recommended) Generate idempotent SQL scripts for review

Producing a script first lets you diff exactly what will run before touching the shared DB.

```powershell
dotnet ef migrations script `
  --project ECTSystem.Persistence `
  --startup-project ECTSystem.Api `
  --context EctDbContext `
  --idempotent `
  --output ./migrations-ect.sql

dotnet ef migrations script `
  --project ECTSystem.Persistence `
  --startup-project ECTSystem.Api `
  --context EctIdentityDbContext `
  --idempotent `
  --output ./migrations-identity.sql
```

Review both `.sql` files. **Wait for explicit user confirmation** before proceeding to step 5.

### 5. Apply migrations to BOTH contexts on REMOTE

> **STOP — require explicit user confirmation ("yes" / "GO") before running this step.**

Choose **one** of the two options below.

**Option A — apply via `dotnet ef` (uses the connection string from `appsettings.Production.json`):**

```powershell
dotnet ef database update `
  --project ECTSystem.Persistence `
  --startup-project ECTSystem.Api `
  --context EctDbContext

dotnet ef database update `
  --project ECTSystem.Persistence `
  --startup-project ECTSystem.Api `
  --context EctIdentityDbContext
```

**Option B — apply the idempotent scripts from step 4 via `sqlcmd`:**

```powershell
sqlcmd -S sql-ect-dev-cus.database.windows.net -d ECT `
  --authentication-method ActiveDirectoryDefault `
  -i ./migrations-ect.sql -b

sqlcmd -S sql-ect-dev-cus.database.windows.net -d ECT `
  --authentication-method ActiveDirectoryDefault `
  -i ./migrations-identity.sql -b
```

If either command fails, stop and surface the error before continuing.

### 6. Verify

```powershell
# EF view of applied migrations
dotnet ef migrations list `
  --project ECTSystem.Persistence `
  --startup-project ECTSystem.Api `
  --context EctDbContext

dotnet ef migrations list `
  --project ECTSystem.Persistence `
  --startup-project ECTSystem.Api `
  --context EctIdentityDbContext

# Cross-check the actual __EFMigrationsHistory rows on REMOTE
sqlcmd -S sql-ect-dev-cus.database.windows.net -d ECT `
  --authentication-method ActiveDirectoryDefault `
  -Q "SELECT TOP 10 MigrationId, ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId DESC" `
  -h -1 -W -b
```

## Notes

- **Two contexts, two histories.** `EctDbContext` and `EctIdentityDbContext` each maintain their own `__EFMigrationsHistory`. Always specify `--context` — omitting it errors out because more than one `DbContext` is registered.
- **Entra auth.** The Production connection string uses `Authentication="Active Directory Default"`. Make sure `az login` (or `azd auth login`) has produced a usable token for the correct tenant before running step 5.
- **No rollback in this prompt.** Schema rollbacks against the shared REMOTE DB require an explicit recovery plan and are intentionally out of scope here. Test rollbacks on LOCAL first via [ef-core-migrate-local.prompt.md](ef-core-migrate-local.prompt.md).
- **Deployment does NOT auto-migrate.** [deploy-api-app-service.prompt.md](deploy-api-app-service.prompt.md) and [deploy-to-azure.prompt.md](deploy-to-azure.prompt.md) ship code only. Run this prompt **before** deploying app code that depends on schema changes.
- **Data reset is a separate concern.** To clear operational rows, use [reset-app-data-remote.prompt.md](reset-app-data-remote.prompt.md).

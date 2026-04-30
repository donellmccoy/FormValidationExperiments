---
name: "EF Core Migrate (LOCAL)"
description: "Apply EF Core migrations to the LOCAL ECT database for both EctDbContext (operational) and EctIdentityDbContext (identity)."
---

Apply pending EF Core migrations to the **LOCAL** SQL Server (`Server=localhost;Database=ECT`) for **both** DbContexts. The Persistence layer hosts two contexts with separate migrations folders, so each `dotnet ef` command MUST specify `--context`.

## Variables

| Name | Value |
|------|-------|
| Migrations project | `ECTSystem.Persistence` |
| Startup project | `ECTSystem.Api` |
| Operational context | `EctDbContext` (migrations dir: `Migrations/`) |
| Identity context | `EctIdentityDbContext` (migrations dir: `Migrations/Identity/`) |
| Connection-string key | `ConnectionStrings:EctDatabase` |
| LOCAL connection (Development) | `Server=localhost;Database=ECT;Trusted_Connection=True;TrustServerCertificate=True` |
| Environment | `Development` |

The `dotnet ef` CLI reads the connection string from the startup project's `appsettings.Development.json` when `ASPNETCORE_ENVIRONMENT=Development`.

## Steps

### 1. Pre-flight — verify `dotnet-ef` global tool

```powershell
dotnet tool list -g | Select-String dotnet-ef
# If missing:
# dotnet tool install --global dotnet-ef
# If outdated:
# dotnet tool update --global dotnet-ef
```

### 2. Target LOCAL environment

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
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

### 4. Apply migrations to BOTH contexts

```powershell
# Operational schema
dotnet ef database update `
  --project ECTSystem.Persistence `
  --startup-project ECTSystem.Api `
  --context EctDbContext

# Identity schema
dotnet ef database update `
  --project ECTSystem.Persistence `
  --startup-project ECTSystem.Api `
  --context EctIdentityDbContext
```

If either command fails, stop and surface the error before continuing.

### 5. Verify

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

# Cross-check the actual __EFMigrationsHistory rows on LOCAL
sqlcmd -S localhost -d ECT -E `
  -Q "SELECT TOP 10 MigrationId, ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId DESC" `
  -h -1 -W -b
```

## Notes

- **Two contexts, two histories.** `EctDbContext` and `EctIdentityDbContext` each maintain their own `__EFMigrationsHistory`. Always specify `--context` — omitting it errors out because more than one `DbContext` is registered.
- **Adding a new migration?** Use `dotnet ef migrations add <Name> --context <Ctx> -o <Migrations|Migrations/Identity>` so the file lands in the correct folder. Review the generated `Up`/`Down` before running this prompt.
- **Data reset is a separate concern.** Migrations only manage schema. To clear operational rows, use [reset-app-data-local.prompt.md](reset-app-data-local.prompt.md).
- **Remote variant.** To apply the same migrations against Azure SQL, use [ef-core-migrate-remote.prompt.md](ef-core-migrate-remote.prompt.md).

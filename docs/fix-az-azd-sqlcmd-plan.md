# Fix Plan: `az`, `azd`, and `sqlcmd -G` on this Workstation

Status: **Draft** — execute top to bottom. Stop and re-test after each section; later steps assume earlier ones succeeded.

## Background

On this machine the Microsoft Entra (Azure AD) auth chain that `sqlcmd -G` relies on is broken because each of its upstream credential providers is broken:

| Tool         | Symptom                                                                 | Root cause                                                                 |
|--------------|-------------------------------------------------------------------------|-----------------------------------------------------------------------------|
| `az`         | Generic load failure / extension errors                                 | Corrupt `az ml` extension under `~/.azure/cliextensions/ml`                 |
| `azd`        | "DPAPI key invalid" decrypting cached credentials                       | Encrypted auth blob no longer matches current Windows DPAPI key             |
| `sqlcmd -G`  | Auth fails before reaching SQL                                          | Falls through `ActiveDirectoryDefault` chain → hits broken `az` / `azd`     |

The VS Code **mssql extension** is unaffected (independent MSAL flow) and remains the safe path until these are fixed — see [reset-app-data-local.prompt.md](../.github/prompts/reset-app-data-local.prompt.md) and [reset-app-data-remote.prompt.md](../.github/prompts/reset-app-data-remote.prompt.md).

Tenant: `dee72072-c1ba-464f-a4f0-9b392ef58d1b`
Subscription: `b1b63908-33c2-4b1d-b3fe-231d103b0d41`

---

## Phase 1 — Repair `az` CLI

### 1.1 Remove the corrupt `ml` extension
```powershell
Remove-Item -Recurse -Force "$env:USERPROFILE\.azure\cliextensions\ml" -ErrorAction SilentlyContinue
```

### 1.2 Verify the core CLI still loads
```powershell
az version
```
- If this succeeds, skip 1.3.
- If it errors, proceed to 1.3.

### 1.3 Repair / upgrade the CLI itself
```powershell
winget upgrade --id Microsoft.AzureCLI -e --silent
# If not currently winget-managed:
# winget install --id Microsoft.AzureCLI -e --silent
```

### 1.4 Re-authenticate
```powershell
az login --tenant dee72072-c1ba-464f-a4f0-9b392ef58d1b
az account set --subscription b1b63908-33c2-4b1d-b3fe-231d103b0d41
az account show
```

### 1.5 (Optional) Reinstall `ml` extension only if needed
```powershell
az extension add --name ml --upgrade
```

**Exit criterion:** `az account show` returns the correct tenant + subscription with no errors.

---

## Phase 2 — Repair `azd`

Skip this phase entirely if you don't use `azd up` / `azd deploy` / `azd env`.

### 2.1 Wipe the corrupt auth cache
```powershell
Remove-Item -Recurse -Force "$env:USERPROFILE\.azd\auth" -ErrorAction SilentlyContinue
```

### 2.2 (Optional) Clear the shared MSAL cache
Only if step 2.4 still fails after 2.1.
```powershell
Remove-Item -Force "$env:LOCALAPPDATA\.IdentityService\msal.cache" -ErrorAction SilentlyContinue
```

### 2.3 Upgrade `azd` itself
```powershell
winget upgrade --id Microsoft.Azd -e --silent
azd version
```

### 2.4 Re-authenticate and set defaults
```powershell
azd auth login --tenant-id dee72072-c1ba-464f-a4f0-9b392ef58d1b
azd config set defaults.subscription b1b63908-33c2-4b1d-b3fe-231d103b0d41
azd auth token --output json | Select-Object -First 1
```

**Exit criterion:** `azd auth token` prints a valid token without DPAPI errors.

---

## Phase 3 — Restore `sqlcmd` Entra auth

Pick **one** option. Option A is preferred (no dependency on `az`/`azd` token caches).

### Option A — Install `go-sqlcmd` (recommended)
The new `go-sqlcmd` ships an independent interactive MSAL flow.

```powershell
winget install --id Microsoft.Sqlcmd -e --silent

# Confirm the new binary is first on PATH
(Get-Command sqlcmd).Source
sqlcmd --version   # legacy sqlcmd does not support --version

# Use it
sqlcmd -S sql-ect-dev-cus.database.windows.net -d ECT `
  --authentication-method ActiveDirectoryInteractive `
  -U you@yourdomain.com `
  -i .\reset-app-data.sql
```

### Option B — Pre-fetched access token (legacy sqlcmd)
Requires Phase 1 to be complete.
```powershell
$token = az account get-access-token `
  --resource https://database.windows.net/ `
  --query accessToken -o tsv

sqlcmd -S sql-ect-dev-cus.database.windows.net -d ECT `
  -G -P $token -b -I -i .\reset-app-data.sql
```

### Option C — Default credential chain (only after Phases 1 & 2 work)
```powershell
sqlcmd -S sql-ect-dev-cus.database.windows.net -d ECT -G -b -I -i .\reset-app-data.sql
```

**Exit criterion:** Chosen command connects and runs the script; the final `SELECT` shows the expected row counts (see [reset-app-data-remote.prompt.md](../.github/prompts/reset-app-data-remote.prompt.md)).

---

## Verification Checklist

- [ ] `az version` clean, no extension load errors.
- [ ] `az account show` returns correct tenant + subscription.
- [ ] (If using azd) `azd auth token` returns a token without DPAPI errors.
- [ ] `sqlcmd` (chosen variant) connects to `sql-ect-dev-cus.database.windows.net` and executes a trivial query (`SELECT @@VERSION`).
- [ ] Re-running the LOCAL and REMOTE reset prompts via the mssql extension still succeeds (regression check — should be unaffected).

---

## Rollback

All steps are non-destructive to source code; the only state removed is local credential cache and one corrupt CLI extension. To roll back:

- `az ml`: `az extension add --name ml` reinstalls it.
- `azd` auth: `azd auth login` re-creates the cache.
- `go-sqlcmd`: `winget uninstall --id Microsoft.Sqlcmd` restores legacy `sqlcmd` precedence (legacy ODBC sqlcmd must still be installed).

No production / shared resources are touched by any step in this plan.

---

## Out of Scope

- Fixing Windows DPAPI itself (would only matter if 2.1 + 2.4 still fail repeatedly — escalate to a profile reset).
- Changing the project's deployment pipeline or CI auth.
- Modifying the reset SQL or the prompt files — those are already validated and in sync.

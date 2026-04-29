---
name: "Reset App Data (REMOTE)"
description: "Clear operational/transactional ECT tables in the REMOTE Azure SQL database (preserves identity, lookups, Members, migrations history). DESTRUCTIVE — confirm before running."
---

Run the data reset script against the **REMOTE** Azure SQL ECT database (`sql-ect-dev-cus.database.windows.net/ECT`) using Microsoft Entra (Azure AD) authentication.

> **WARNING**: This is destructive against the shared dev database. Confirm with the team before executing.

Execute the following command in a terminal at the workspace root:

```powershell
sqlcmd -S sql-ect-dev-cus.database.windows.net -d ECT -G -b -I -i .\reset-app-data.sql
```

Notes:
- `-G` enables Microsoft Entra authentication (interactive / Default credential).
- `-I` enables QUOTED_IDENTIFIER (required by filtered/computed indexes).
- `-b` exits with a non-zero code on SQL error so failures surface in the terminal.
- The script runs inside a single transaction with `SET XACT_ABORT ON`; any failure rolls back.
- Script: [reset-app-data.sql](../../reset-app-data.sql)

If `-G` fails to authenticate, try `-G -U <your-upn>@<tenant>` to force interactive login, or use `-G -P <token>` with an Azure AD access token retrieved via `az account get-access-token --resource https://database.windows.net/`.

After running, verify the row-count summary printed at the end shows `0` for all transactional tables and non-zero for `AspNetUsers`, `WorkflowStates`, `WorkflowTypes`, `WorkflowModules`, `Members`, and `__EFMigrationsHistory`.

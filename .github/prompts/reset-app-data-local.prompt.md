---
name: "Reset App Data (LOCAL)"
description: "Clear operational/transactional ECT tables in the LOCAL SQL Server database (preserves identity, lookups, Members, migrations history)."
---

Run the data reset script against the **LOCAL** ECT database (`Server=localhost;Database=ECT;Trusted_Connection=True`).

Execute the following command in a terminal at the workspace root:

```powershell
sqlcmd -S localhost -d ECT -E -b -I -i .\reset-app-data.sql
```

Notes:
- `-E` uses Windows authentication (Trusted_Connection).
- `-I` enables QUOTED_IDENTIFIER (required by filtered/computed indexes).
- `-b` exits with a non-zero code on SQL error so failures surface in the terminal.
- The script runs inside a single transaction with `SET XACT_ABORT ON`; any failure rolls back.
- Script: [reset-app-data.sql](../../reset-app-data.sql)

After running, verify the row-count summary printed at the end shows `0` for all transactional tables and non-zero for `AspNetUsers`, `WorkflowStates`, `WorkflowTypes`, `WorkflowModules`, `Members`, and `__EFMigrationsHistory`.

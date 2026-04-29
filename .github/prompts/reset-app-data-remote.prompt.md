---
name: "Reset App Data (REMOTE)"
description: "Clear operational/transactional ECT tables in the REMOTE Azure SQL database (preserves identity, lookups, Members, migrations history). DESTRUCTIVE — confirm before running."
---

Run the embedded data reset script against the **REMOTE** Azure SQL ECT database (`sql-ect-dev-cus.database.windows.net/ECT`) using the **VS Code mssql extension** MCP tools (Microsoft Entra auth via the extension's MSAL flow).

> **WARNING**: This is destructive against the shared dev database. **Require explicit user confirmation (e.g. "yes" / "GO") before invoking step 2 below.**

## Steps

1. **Connect** to the remote ECT database using the saved profile **"ECT - REMOTE"**:

	- Tool: `mssql_connect`
	- Parameters:
	  - `serverName`: `sql-ect-dev-cus.database.windows.net`
	  - `database`: `ECT`
	  - `profileId`: `B2B3AE81-568B-4848-A1D0-A3E4FDF99C9E`
	- Capture the returned `connectionId`.

2. **Wait for explicit user confirmation**, then **run** the embedded SQL below in a single batch:

	- Tool: `mssql_run_query`
	- Parameters:
	  - `connectionId`: from step 1
	  - `queryIntent`: `data_maintenance`
	  - `queryTypes`: `["DELETE","UPDATE","TRANSACTION","DECLARE","WHILE","SELECT","OTHER"]`
	  - `query`: the SQL block below (verbatim)

3. **Verify** the final `SELECT` result shows:
	- `0` rows for all operational tables: `Appeals`, `AuditComments`, `Authorities`, `Bookmarks`, `CaseDialogueComments`, `Cases`, `Documents`, `INCAPDetails`, `MEDCONDetails`, `Notifications`, `WitnessStatements`, `WorkflowStateHistory`.
	- Preserved (non-zero): `__EFMigrationsHistory` (56), `AspNetUsers`, `Members` (400), `WorkflowStates` (24), `WorkflowTypes` (2), `WorkflowModules` (2).

## Embedded SQL

```sql
-- Clears operational/transactional ECT data so the application starts in a "first use" state.
-- Preserves: __EFMigrationsHistory, AspNet* identity tables, lookup tables (WorkflowTypes,
-- WorkflowStates, WorkflowModules), and Members (reference roster seeded by EctDbSeeder).
-- Single batch (no GO) — safe for mssql_run_query.

SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

-- Break Cases -> INCAPDetails / MEDCONDetails FK references so the detail rows can be deleted.
UPDATE dbo.Cases SET INCAPId = NULL WHERE INCAPId IS NOT NULL;
UPDATE dbo.Cases SET MEDCONId = NULL WHERE MEDCONId IS NOT NULL;

-- Children first (FK-safe order)
DELETE FROM dbo.CaseDialogueComments;
DELETE FROM dbo.AuditComments;
DELETE FROM dbo.WitnessStatements;
DELETE FROM dbo.Documents;
DELETE FROM dbo.Appeals;
DELETE FROM dbo.Authorities;
DELETE FROM dbo.INCAPDetails;
DELETE FROM dbo.MEDCONDetails;
DELETE FROM dbo.Notifications;
DELETE FROM dbo.WorkflowStateHistory;
DELETE FROM dbo.Bookmarks;
DELETE FROM dbo.Cases;

-- Reseed identity columns where applicable (ignore tables without IDENTITY)
DECLARE @t sysname;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR
	SELECT name FROM (VALUES
		('CaseDialogueComments'),
		('AuditComments'),
		('WitnessStatements'),
		('Documents'),
		('Appeals'),
		('Authorities'),
		('INCAPDetails'),
		('MEDCONDetails'),
		('Notifications'),
		('WorkflowStateHistory'),
		('Bookmarks'),
		('Cases')
	) AS x(name);
OPEN c;
FETCH NEXT FROM c INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
	IF OBJECTPROPERTY(OBJECT_ID(N'dbo.' + @t), 'TableHasIdentity') = 1
		DBCC CHECKIDENT (@t, RESEED, 0) WITH NO_INFOMSGS;
	FETCH NEXT FROM c INTO @t;
END
CLOSE c;
DEALLOCATE c;

COMMIT TRANSACTION;

-- Verify
SELECT t.name AS TableName, p.rows AS Rows
FROM sys.tables t
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
WHERE t.is_ms_shipped = 0
ORDER BY t.name;
```

## Notes

- The script runs inside a single transaction with `SET XACT_ABORT ON`; any failure rolls back.
- Single batch — no `GO` separators, compatible with `mssql_run_query`.
- The mssql VS Code extension performs Entra auth via its own MSAL flow, independent of the `az`/`azd` CLI credential chain. Do **not** fall back to `sqlcmd -G` / `--authentication-method ActiveDirectoryDefault` on this machine — those paths are known broken.
- The standalone file [reset-app-data.sql](../../reset-app-data.sql) is kept in sync with the SQL above.

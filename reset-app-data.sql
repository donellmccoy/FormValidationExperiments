-- reset-app-data.sql
-- Clears operational/transactional ECT data so the application starts in a "first use" state.
-- Preserves: __EFMigrationsHistory, AspNet* identity tables, lookup tables (WorkflowTypes,
-- WorkflowStates, WorkflowModules), and Members (reference roster seeded by EctDbSeeder).
--
-- Run with sqlcmd using the -I flag (QUOTED_IDENTIFIER ON):
--   sqlcmd -S <server> -d ECT -E -b -I -i .\reset-app-data.sql      (local, Windows auth)
--   sqlcmd -S <server> -d ECT -G -b -I -i .\reset-app-data.sql      (Azure SQL, Entra auth)

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

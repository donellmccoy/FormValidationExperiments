-- ============================================================
-- PrepareDatabase.sql
-- Prepares the ECT database for first use by clearing all
-- transactional data while preserving reference/lookup data
-- (WorkflowModules, WorkflowTypes, WorkflowStates) and
-- Identity tables (AspNetUsers, AspNetRoles, etc.).
-- Tables are deleted in FK-dependency order (children first).
-- Identity seeds are reset to 0 (next insert gets Id = 1).
-- The EF Migrations history tables are NOT touched.
-- ============================================================
-- Target: localhost, Database: ect
-- ============================================================

USE [ect];
GO

SET NOCOUNT ON;

BEGIN TRANSACTION;
BEGIN TRY

    -- --------------------------------------------------------
    -- 1. Case child tables (all FK -> Cases)
    --    Appeals also has FK -> Authorities, so delete first.
    -- --------------------------------------------------------
    DELETE FROM [dbo].[Appeals];
    DELETE FROM [dbo].[AuditComments];
    DELETE FROM [dbo].[CaseDialogueComments];
    DELETE FROM [dbo].[Authorities];
    DELETE FROM [dbo].[WorkflowStateHistory];
    DELETE FROM [dbo].[Bookmarks];
    DELETE FROM [dbo].[Documents];
    DELETE FROM [dbo].[Notifications];
    DELETE FROM [dbo].[WitnessStatements];

    -- --------------------------------------------------------
    -- 2. Root aggregate (FK -> Members, INCAPDetails, MEDCONDetails)
    -- --------------------------------------------------------
    DELETE FROM [dbo].[Cases];

    -- --------------------------------------------------------
    -- 3. Root tables (no FK parents in domain model)
    -- --------------------------------------------------------
    DELETE FROM [dbo].[INCAPDetails];
    DELETE FROM [dbo].[MEDCONDetails];

    -- --------------------------------------------------------
    -- 4. Reseed identity columns (0 = next insert gets Id 1)
    -- --------------------------------------------------------
    DBCC CHECKIDENT ('[dbo].[Appeals]',               RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[AuditComments]',         RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[CaseDialogueComments]',  RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[Authorities]',           RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[WorkflowStateHistory]',  RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[Bookmarks]',             RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[Documents]',             RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[Notifications]',         RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[WitnessStatements]',     RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[Cases]',                 RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[INCAPDetails]',          RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[MEDCONDetails]',         RESEED, 0);

    -- --------------------------------------------------------
    -- 5. Verify reference/lookup data is intact
    -- --------------------------------------------------------
    DECLARE @modules INT, @types INT, @states INT;
    SELECT @modules = COUNT(*) FROM [dbo].[WorkflowModules];
    SELECT @types   = COUNT(*) FROM [dbo].[WorkflowTypes];
    SELECT @states  = COUNT(*) FROM [dbo].[WorkflowStates];

    IF @modules = 0 OR @types = 0 OR @states = 0
    BEGIN
        PRINT 'WARNING: Reference data is missing. Run EF migrations or recreate-missing-tables.sql.';
        PRINT '  WorkflowModules: ' + CAST(@modules AS NVARCHAR(10));
        PRINT '  WorkflowTypes:   ' + CAST(@types   AS NVARCHAR(10));
        PRINT '  WorkflowStates:  ' + CAST(@states  AS NVARCHAR(10));
    END
    ELSE
    BEGIN
        PRINT 'Reference data OK:';
        PRINT '  WorkflowModules: ' + CAST(@modules AS NVARCHAR(10));
        PRINT '  WorkflowTypes:   ' + CAST(@types   AS NVARCHAR(10));
        PRINT '  WorkflowStates:  ' + CAST(@states  AS NVARCHAR(10));
    END

    COMMIT TRANSACTION;
    PRINT '';
    PRINT 'Database prepared successfully. All transactional data cleared.';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH
GO

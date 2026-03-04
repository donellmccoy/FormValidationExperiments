-- ============================================================
-- ClearDatabase.sql
-- Deletes all data from the ECT database for first use.
-- Tables are deleted in FK-dependency order (children first).
-- Identity seeds are reset to 1.
-- The EF Migrations history table is NOT touched.
-- ============================================================

USE [ect];
GO

BEGIN TRANSACTION;
BEGIN TRY

    -- --------------------------------------------------------
    -- 1. Domain child tables (depend on Cases / Authorities)
    -- --------------------------------------------------------
    DELETE FROM [dbo].[TimelineSteps];
    DELETE FROM [dbo].[Appeals];
    DELETE FROM [dbo].[Authorities];
    DELETE FROM [dbo].[WorkflowStateHistories];
    DELETE FROM [dbo].[CaseBookmarks];
    DELETE FROM [dbo].[Documents];
    DELETE FROM [dbo].[Notifications];

    -- --------------------------------------------------------
    -- 2. Root aggregate (depends on Members, INCAPDetails, MEDCONDetails)
    -- --------------------------------------------------------
    DELETE FROM [dbo].[Cases];

    -- --------------------------------------------------------
    -- 3. Domain root tables (no FK parents)
    -- --------------------------------------------------------
    DELETE FROM [dbo].[INCAPDetails];
    DELETE FROM [dbo].[MEDCONDetails];

    -- --------------------------------------------------------
    -- 4. Reseed identity columns
    -- --------------------------------------------------------
    DBCC CHECKIDENT ('[dbo].[TimelineSteps]',   RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[Appeals]',          RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[Authorities]',      RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[WorkflowStateHistories]', RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[CaseBookmarks]',    RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[Documents]',        RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[Notifications]',    RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[Cases]',            RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[INCAPDetails]',     RESEED, 0);
    DBCC CHECKIDENT ('[dbo].[MEDCONDetails]',    RESEED, 0);

    COMMIT TRANSACTION;
    PRINT 'Database cleared successfully.';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error: ' + ERROR_MESSAGE();
    THROW;
END CATCH
GO

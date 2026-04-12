-- Recreate missing WorkflowModules, WorkflowTypes, WorkflowStates tables
-- These tables were removed from the database outside of EF migrations
-- and must exist before the pending ConstrainNvarcharMaxColumns migration can run.

-- 1. WorkflowModules (no FK dependencies)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WorkflowModules')
BEGIN
    CREATE TABLE [WorkflowModules] (
        [Id] int NOT NULL IDENTITY(1,1),
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [CreatedBy] int NOT NULL DEFAULT 0,
        [CreatedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
        [ModifiedBy] int NOT NULL DEFAULT 0,
        [ModifiedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_WorkflowModules] PRIMARY KEY ([Id])
    );

    CREATE UNIQUE INDEX [IX_WorkflowModules_Name] ON [WorkflowModules] ([Name]);

    SET IDENTITY_INSERT [WorkflowModules] ON;
    INSERT INTO [WorkflowModules] ([Id], [Name], [Description], [CreatedBy], [ModifiedBy])
    VALUES (1, N'AFRC', N'Air Force Reserve Command workflow module.', 0, 0),
           (2, N'ANG', N'Air National Guard workflow module.', 0, 0);
    SET IDENTITY_INSERT [WorkflowModules] OFF;

    PRINT 'Created WorkflowModules table with seed data.';
END
ELSE
    PRINT 'WorkflowModules table already exists - skipped.';
GO

-- 2. WorkflowTypes (FK -> WorkflowModules)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WorkflowTypes')
BEGIN
    CREATE TABLE [WorkflowTypes] (
        [Id] int NOT NULL IDENTITY(1,1),
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [WorkflowModuleId] int NOT NULL DEFAULT 0,
        [CreatedBy] int NOT NULL DEFAULT 0,
        [CreatedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
        [ModifiedBy] int NOT NULL DEFAULT 0,
        [ModifiedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_WorkflowTypes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkflowTypes_WorkflowModules_WorkflowModuleId] FOREIGN KEY ([WorkflowModuleId]) REFERENCES [WorkflowModules]([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_WorkflowTypes_WorkflowModuleId] ON [WorkflowTypes] ([WorkflowModuleId]);
    CREATE UNIQUE INDEX [IX_WorkflowTypes_Name_WorkflowModuleId] ON [WorkflowTypes] ([Name], [WorkflowModuleId]);

    SET IDENTITY_INSERT [WorkflowTypes] ON;
    INSERT INTO [WorkflowTypes] ([Id], [Name], [Description], [WorkflowModuleId], [CreatedBy], [ModifiedBy])
    VALUES (1, N'Informal', N'Informal LOD determination process.', 1, 0, 0),
           (2, N'Formal', N'Formal LOD determination process.', 1, 0, 0);
    SET IDENTITY_INSERT [WorkflowTypes] OFF;

    PRINT 'Created WorkflowTypes table with seed data.';
END
ELSE
    PRINT 'WorkflowTypes table already exists - skipped.';
GO

-- 3. WorkflowStates (FK -> WorkflowTypes)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WorkflowStates')
BEGIN
    CREATE TABLE [WorkflowStates] (
        [Id] int NOT NULL IDENTITY(1,1),
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [DisplayOrder] int NOT NULL DEFAULT 0,
        [WorkflowTypeId] int NOT NULL DEFAULT 0,
        [CreatedBy] int NOT NULL DEFAULT 0,
        [CreatedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
        [ModifiedBy] int NOT NULL DEFAULT 0,
        [ModifiedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_WorkflowStates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkflowStates_WorkflowTypes_WorkflowTypeId] FOREIGN KEY ([WorkflowTypeId]) REFERENCES [WorkflowTypes]([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_WorkflowStates_WorkflowTypeId] ON [WorkflowStates] ([WorkflowTypeId]);
    CREATE UNIQUE INDEX [IX_WorkflowStates_Name_WorkflowTypeId] ON [WorkflowStates] ([Name], [WorkflowTypeId]);

    SET IDENTITY_INSERT [WorkflowStates] ON;
    INSERT INTO [WorkflowStates] ([Id], [Name], [Description], [DisplayOrder], [WorkflowTypeId], [CreatedBy], [ModifiedBy])
    VALUES
        (1,  N'Member Information Entry',    N'Enter member identification and incident details to initiate the LOD case.', 1, 1, 0, 0),
        (2,  N'Medical Technician Review',   N'Medical technician reviews the injury/illness and documents clinical findings.', 2, 1, 0, 0),
        (3,  N'Medical Officer Review',      N'Medical officer reviews the technician''s findings and provides a clinical assessment.', 3, 1, 0, 0),
        (4,  N'Unit CC Review',              N'Unit commander reviews the case and submits a recommendation for the LOD determination.', 4, 1, 0, 0),
        (5,  N'Wing JA Review',              N'Wing Judge Advocate reviews the case for legal sufficiency and compliance.', 5, 1, 0, 0),
        (6,  N'Appointing Authority Review', N'Appointing authority reviews the case and issues a formal LOD determination.', 6, 1, 0, 0),
        (7,  N'Wing CC Review',              N'Wing commander reviews the case and renders a preliminary LOD determination.', 7, 1, 0, 0),
        (8,  N'Board Technician Review',     N'Board medical technician reviews the case file for completeness and accuracy.', 8, 1, 0, 0),
        (9,  N'Board Medical Review',        N'Board medical officer reviews all medical evidence and provides a formal assessment.', 9, 1, 0, 0),
        (10, N'Board Legal Review',          N'Board legal counsel reviews the case for legal sufficiency before final decision.', 10, 1, 0, 0),
        (11, N'Board Admin Review',          N'Board administrative officer finalizes the case package and prepares the formal determination.', 11, 1, 0, 0),
        (12, N'Completed',                   N'LOD determination has been finalized and the case is closed.', 12, 1, 0, 0),
        (13, N'Member Information Entry',    N'Enter member identification and incident details to initiate the LOD case.', 1, 2, 0, 0),
        (14, N'Medical Technician Review',   N'Medical technician reviews the injury/illness and documents clinical findings.', 2, 2, 0, 0),
        (15, N'Medical Officer Review',      N'Medical officer reviews the technician''s findings and provides a clinical assessment.', 3, 2, 0, 0),
        (16, N'Unit CC Review',              N'Unit commander reviews the case and submits a recommendation for the LOD determination.', 4, 2, 0, 0),
        (17, N'Wing JA Review',              N'Wing Judge Advocate reviews the case for legal sufficiency and compliance.', 5, 2, 0, 0),
        (18, N'Appointing Authority Review', N'Appointing authority reviews the case and issues a formal LOD determination.', 6, 2, 0, 0),
        (19, N'Wing CC Review',              N'Wing commander reviews the case and renders a preliminary LOD determination.', 7, 2, 0, 0),
        (20, N'Board Technician Review',     N'Board medical technician reviews the case file for completeness and accuracy.', 8, 2, 0, 0),
        (21, N'Board Medical Review',        N'Board medical officer reviews all medical evidence and provides a formal assessment.', 9, 2, 0, 0),
        (22, N'Board Legal Review',          N'Board legal counsel reviews the case for legal sufficiency before final decision.', 10, 2, 0, 0),
        (23, N'Board Admin Review',          N'Board administrative officer finalizes the case package and prepares the formal determination.', 11, 2, 0, 0),
        (24, N'Completed',                   N'LOD determination has been finalized and the case is closed.', 12, 2, 0, 0);
    SET IDENTITY_INSERT [WorkflowStates] OFF;

    PRINT 'Created WorkflowStates table with seed data.';
END
ELSE
    PRINT 'WorkflowStates table already exists - skipped.';
GO

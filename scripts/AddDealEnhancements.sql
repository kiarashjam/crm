BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209103032_AddDealEnhancements'
)
BEGIN
    ALTER TABLE [TaskItems] ADD [ReminderSentAtUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209103032_AddDealEnhancements'
)
BEGIN
    ALTER TABLE [Deals] ADD [ClosedAtUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209103032_AddDealEnhancements'
)
BEGIN
    ALTER TABLE [Deals] ADD [ClosedReason] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209103032_AddDealEnhancements'
)
BEGIN
    ALTER TABLE [Deals] ADD [ClosedReasonCategory] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209103032_AddDealEnhancements'
)
BEGIN
    ALTER TABLE [Deals] ADD [Description] nvarchar(2000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209103032_AddDealEnhancements'
)
BEGIN
    ALTER TABLE [Deals] ADD [Probability] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209103032_AddDealEnhancements'
)
BEGIN
    ALTER TABLE [Contacts] ADD [Description] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209103032_AddDealEnhancements'
)
BEGIN
    ALTER TABLE [Companies] ADD [Description] nvarchar(2000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209103032_AddDealEnhancements'
)
BEGIN
    ALTER TABLE [Companies] ADD [Location] nvarchar(300) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209103032_AddDealEnhancements'
)
BEGIN
    ALTER TABLE [Companies] ADD [Website] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209103032_AddDealEnhancements'
)
BEGIN
    CREATE TABLE [DealStageChanges] (
        [Id] uniqueidentifier NOT NULL,
        [DealId] uniqueidentifier NOT NULL,
        [FromDealStageId] uniqueidentifier NULL,
        [FromStageName] nvarchar(128) NULL,
        [ToDealStageId] uniqueidentifier NULL,
        [ToStageName] nvarchar(128) NULL,
        [ChangedByUserId] uniqueidentifier NOT NULL,
        [ChangedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DealStageChanges] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DealStageChanges_Deals_DealId] FOREIGN KEY ([DealId]) REFERENCES [Deals] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DealStageChanges_Users_ChangedByUserId] FOREIGN KEY ([ChangedByUserId]) REFERENCES [Users] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209103032_AddDealEnhancements'
)
BEGIN
    CREATE TABLE [TaskComments] (
        [Id] uniqueidentifier NOT NULL,
        [TaskItemId] uniqueidentifier NOT NULL,
        [AuthorId] uniqueidentifier NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_TaskComments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskComments_TaskItems_TaskItemId] FOREIGN KEY ([TaskItemId]) REFERENCES [TaskItems] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TaskComments_Users_AuthorId] FOREIGN KEY ([AuthorId]) REFERENCES [Users] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209103032_AddDealEnhancements'
)
BEGIN
    CREATE INDEX [IX_DealStageChanges_ChangedByUserId] ON [DealStageChanges] ([ChangedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209103032_AddDealEnhancements'
)
BEGIN
    CREATE INDEX [IX_DealStageChanges_DealId] ON [DealStageChanges] ([DealId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209103032_AddDealEnhancements'
)
BEGIN
    CREATE INDEX [IX_TaskComments_AuthorId] ON [TaskComments] ([AuthorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209103032_AddDealEnhancements'
)
BEGIN
    CREATE INDEX [IX_TaskComments_TaskItemId] ON [TaskComments] ([TaskItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209103032_AddDealEnhancements'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260209103032_AddDealEnhancements', N'8.0.11');
END;
GO

COMMIT;
GO


-- The schema the migrations made before the baseline migration replaced them: the four
-- EF discovered (InitialCreate, AddDealEnhancements, AddOrganizationWebhookPassword and
-- SpeedUpLeadAndActivityLookups), in the order MigrateAsync ran them, history rows
-- included. Generated with `dotnet ef migrations script` from commit 964bdfd, the last
-- before the baseline. Every database made before the baseline started from these and
-- got the rest from the start-up DDL in Program.cs; SchemaMigrationTests rebuilds such a
-- database from this file and starts the API on it.

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [Users] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(256) NOT NULL,
    [Email] nvarchar(256) NOT NULL,
    [PasswordHash] nvarchar(500) NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [TwoFactorSecretProtected] nvarchar(2000) NULL,
    [TwoFactorEnabledAtUtc] datetime2 NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [LastLoginAtUtc] datetime2 NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Organizations] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(256) NOT NULL,
    [OwnerUserId] uniqueidentifier NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [WebhookApiKey] nvarchar(max) NULL,
    [WebhookApiKeyCreatedAtUtc] datetime2 NULL,
    CONSTRAINT [PK_Organizations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Organizations_Users_OwnerUserId] FOREIGN KEY ([OwnerUserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [UserSettings] (
    [UserId] uniqueidentifier NOT NULL,
    [CompanyName] nvarchar(256) NOT NULL,
    [JobTitle] nvarchar(128) NULL,
    [AvatarUrl] nvarchar(512) NULL,
    [Phone] nvarchar(32) NULL,
    [Timezone] nvarchar(64) NOT NULL DEFAULT N'UTC',
    [Language] nvarchar(10) NOT NULL DEFAULT N'en',
    [Bio] nvarchar(500) NULL,
    [BrandTone] int NOT NULL,
    [EmailSignature] nvarchar(2000) NULL,
    [DefaultEmailSubjectPrefix] nvarchar(100) NULL,
    [Theme] int NOT NULL,
    [DataDensity] int NOT NULL,
    [SidebarCollapsed] bit NOT NULL,
    [ShowWelcomeBanner] bit NOT NULL,
    [EmailNotificationsEnabled] bit NOT NULL,
    [EmailOnNewLead] bit NOT NULL,
    [EmailOnDealUpdate] bit NOT NULL,
    [EmailOnTaskDue] bit NOT NULL,
    [EmailOnTeamMention] bit NOT NULL,
    [EmailDigestFrequency] int NOT NULL,
    [InAppNotificationsEnabled] bit NOT NULL,
    [InAppSoundEnabled] bit NOT NULL,
    [BrowserNotificationsEnabled] bit NOT NULL,
    [DefaultPipelineId] nvarchar(64) NULL,
    [DefaultLeadStatusId] nvarchar(64) NULL,
    [DefaultLeadSourceId] nvarchar(64) NULL,
    [DefaultFollowUpDays] int NOT NULL DEFAULT 3,
    [DefaultCurrency] nvarchar(10) NULL DEFAULT N'USD',
    [ShowActivityStatus] bit NOT NULL,
    [AllowAnalytics] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_UserSettings] PRIMARY KEY ([UserId]),
    CONSTRAINT [FK_UserSettings_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Companies] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [OrganizationId] uniqueidentifier NULL,
    [Name] nvarchar(256) NOT NULL,
    [Domain] nvarchar(256) NULL,
    [Industry] nvarchar(128) NULL,
    [Size] nvarchar(64) NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    CONSTRAINT [PK_Companies] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Companies_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]),
    CONSTRAINT [FK_Companies_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_Companies_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [CopyHistoryItems] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [OrganizationId] uniqueidentifier NULL,
    [Type] nvarchar(128) NOT NULL,
    [Copy] nvarchar(max) NOT NULL,
    [Subject] nvarchar(max) NULL,
    [RecipientName] nvarchar(256) NOT NULL,
    [RecipientEmail] nvarchar(max) NULL,
    [RecipientType] int NOT NULL,
    [RecipientId] nvarchar(128) NOT NULL,
    [CopyTypeId] int NOT NULL,
    [Goal] nvarchar(max) NULL,
    [BrandTone] nvarchar(max) NULL,
    [IsRewritten] bit NOT NULL,
    [CopyCount] int NOT NULL,
    [SendCount] int NOT NULL,
    [ResponseCount] int NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CopyHistoryItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CopyHistoryItems_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]),
    CONSTRAINT [FK_CopyHistoryItems_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [EmailSequences] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(256) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [UserId] uniqueidentifier NOT NULL,
    [OrganizationId] uniqueidentifier NULL,
    [IsActive] bit NOT NULL,
    [IsSharedWithOrganization] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NULL,
    CONSTRAINT [PK_EmailSequences] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmailSequences_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_EmailSequences_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Invites] (
    [Id] uniqueidentifier NOT NULL,
    [OrganizationId] uniqueidentifier NOT NULL,
    [Email] nvarchar(256) NOT NULL,
    [Token] nvarchar(128) NOT NULL,
    [ExpiresAtUtc] datetime2 NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [AcceptedByUserId] uniqueidentifier NULL,
    [AcceptedAtUtc] datetime2 NULL,
    CONSTRAINT [PK_Invites] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Invites_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [JoinRequests] (
    [Id] uniqueidentifier NOT NULL,
    [OrganizationId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Status] nvarchar(32) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [RespondedByUserId] uniqueidentifier NULL,
    [RespondedAtUtc] datetime2 NULL,
    CONSTRAINT [PK_JoinRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_JoinRequests_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_JoinRequests_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [LeadSources] (
    [Id] uniqueidentifier NOT NULL,
    [OrganizationId] uniqueidentifier NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [DisplayOrder] int NOT NULL,
    CONSTRAINT [PK_LeadSources] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LeadSources_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [LeadStatuses] (
    [Id] uniqueidentifier NOT NULL,
    [OrganizationId] uniqueidentifier NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [DisplayOrder] int NOT NULL,
    CONSTRAINT [PK_LeadStatuses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LeadStatuses_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [OrganizationMembers] (
    [OrganizationId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Role] nvarchar(32) NOT NULL,
    [JoinedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_OrganizationMembers] PRIMARY KEY ([OrganizationId], [UserId]),
    CONSTRAINT [FK_OrganizationMembers_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_OrganizationMembers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [OrgSettings] (
    [OrganizationId] uniqueidentifier NOT NULL,
    [CompanyName] nvarchar(256) NOT NULL,
    [BrandTone] nvarchar(32) NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_OrgSettings] PRIMARY KEY ([OrganizationId]),
    CONSTRAINT [FK_OrgSettings_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Pipelines] (
    [Id] uniqueidentifier NOT NULL,
    [OrganizationId] uniqueidentifier NOT NULL,
    [Name] nvarchar(256) NOT NULL,
    [DisplayOrder] int NOT NULL,
    CONSTRAINT [PK_Pipelines] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Pipelines_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Templates] (
    [Id] uniqueidentifier NOT NULL,
    [Title] nvarchar(256) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [Category] nvarchar(128) NOT NULL,
    [CopyTypeId] int NOT NULL,
    [Goal] nvarchar(512) NOT NULL,
    [Content] nvarchar(4000) NULL,
    [BrandTone] nvarchar(64) NULL,
    [Length] nvarchar(32) NULL,
    [UseCount] int NOT NULL,
    [UserId] uniqueidentifier NULL,
    [OrganizationId] uniqueidentifier NULL,
    [IsSharedWithOrganization] bit NOT NULL DEFAULT CAST(0 AS bit),
    [IsSystemTemplate] bit NOT NULL DEFAULT CAST(0 AS bit),
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NULL,
    CONSTRAINT [PK_Templates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Templates_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]),
    CONSTRAINT [FK_Templates_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [EmailSequenceSteps] (
    [Id] uniqueidentifier NOT NULL,
    [SequenceId] uniqueidentifier NOT NULL,
    [StepOrder] int NOT NULL,
    [Subject] nvarchar(500) NOT NULL,
    [Body] nvarchar(max) NOT NULL,
    [CopyTypeId] nvarchar(64) NOT NULL,
    [DelayDays] int NOT NULL,
    [DelayHours] int NOT NULL,
    [StopOnReply] bit NOT NULL,
    CONSTRAINT [PK_EmailSequenceSteps] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmailSequenceSteps_EmailSequences_SequenceId] FOREIGN KEY ([SequenceId]) REFERENCES [EmailSequences] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [DealStages] (
    [Id] uniqueidentifier NOT NULL,
    [PipelineId] uniqueidentifier NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [DisplayOrder] int NOT NULL,
    [IsWon] bit NOT NULL,
    [IsLost] bit NOT NULL,
    CONSTRAINT [PK_DealStages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DealStages_Pipelines_PipelineId] FOREIGN KEY ([PipelineId]) REFERENCES [Pipelines] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Activities] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [OrganizationId] uniqueidentifier NULL,
    [Type] nvarchar(32) NOT NULL,
    [Subject] nvarchar(512) NULL,
    [Body] nvarchar(max) NULL,
    [ContactId] uniqueidentifier NULL,
    [DealId] uniqueidentifier NULL,
    [LeadId] uniqueidentifier NULL,
    [Participants] nvarchar(1024) NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    CONSTRAINT [PK_Activities] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Activities_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]),
    CONSTRAINT [FK_Activities_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_Activities_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Contacts] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [OrganizationId] uniqueidentifier NULL,
    [Name] nvarchar(256) NOT NULL,
    [Email] nvarchar(256) NOT NULL,
    [Phone] nvarchar(64) NULL,
    [JobTitle] nvarchar(256) NULL,
    [CompanyId] uniqueidentifier NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    [ConvertedFromLeadId] uniqueidentifier NULL,
    [ConvertedAtUtc] datetime2 NULL,
    [IsArchived] bit NOT NULL,
    [ArchivedAtUtc] datetime2 NULL,
    [ArchivedByUserId] uniqueidentifier NULL,
    [DoNotContact] bit NOT NULL,
    [PreferredContactMethod] nvarchar(32) NULL,
    CONSTRAINT [PK_Contacts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Contacts_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]),
    CONSTRAINT [FK_Contacts_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]),
    CONSTRAINT [FK_Contacts_Users_ArchivedByUserId] FOREIGN KEY ([ArchivedByUserId]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_Contacts_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_Contacts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Deals] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [OrganizationId] uniqueidentifier NULL,
    [Name] nvarchar(512) NOT NULL,
    [Value] nvarchar(64) NOT NULL,
    [Currency] nvarchar(8) NULL,
    [Stage] nvarchar(128) NULL,
    [PipelineId] uniqueidentifier NULL,
    [DealStageId] uniqueidentifier NULL,
    [CompanyId] uniqueidentifier NULL,
    [ContactId] uniqueidentifier NULL,
    [AssigneeId] uniqueidentifier NULL,
    [ExpectedCloseDateUtc] datetime2 NULL,
    [IsWon] bit NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    CONSTRAINT [PK_Deals] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Deals_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]),
    CONSTRAINT [FK_Deals_Contacts_ContactId] FOREIGN KEY ([ContactId]) REFERENCES [Contacts] ([Id]),
    CONSTRAINT [FK_Deals_DealStages_DealStageId] FOREIGN KEY ([DealStageId]) REFERENCES [DealStages] ([Id]),
    CONSTRAINT [FK_Deals_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]),
    CONSTRAINT [FK_Deals_Pipelines_PipelineId] FOREIGN KEY ([PipelineId]) REFERENCES [Pipelines] ([Id]),
    CONSTRAINT [FK_Deals_Users_AssigneeId] FOREIGN KEY ([AssigneeId]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_Deals_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_Deals_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Leads] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [OrganizationId] uniqueidentifier NULL,
    [Name] nvarchar(256) NOT NULL,
    [Email] nvarchar(256) NOT NULL,
    [Phone] nvarchar(64) NULL,
    [CompanyId] uniqueidentifier NULL,
    [Source] nvarchar(64) NULL,
    [Status] nvarchar(64) NOT NULL,
    [LeadSourceId] uniqueidentifier NULL,
    [LeadStatusId] uniqueidentifier NULL,
    [LeadScore] int NULL,
    [LastContactedAt] datetime2 NULL,
    [Description] nvarchar(2000) NULL,
    [LifecycleStage] nvarchar(64) NULL,
    [IsConverted] bit NOT NULL,
    [ConvertedAtUtc] datetime2 NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    [ConvertedToContactId] uniqueidentifier NULL,
    [ConvertedToDealId] uniqueidentifier NULL,
    [ConvertedToCompanyId] uniqueidentifier NULL,
    CONSTRAINT [PK_Leads] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Leads_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]),
    CONSTRAINT [FK_Leads_Deals_ConvertedToDealId] FOREIGN KEY ([ConvertedToDealId]) REFERENCES [Deals] ([Id]),
    CONSTRAINT [FK_Leads_LeadSources_LeadSourceId] FOREIGN KEY ([LeadSourceId]) REFERENCES [LeadSources] ([Id]),
    CONSTRAINT [FK_Leads_LeadStatuses_LeadStatusId] FOREIGN KEY ([LeadStatusId]) REFERENCES [LeadStatuses] ([Id]),
    CONSTRAINT [FK_Leads_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]),
    CONSTRAINT [FK_Leads_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_Leads_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [EmailSequenceEnrollments] (
    [Id] uniqueidentifier NOT NULL,
    [SequenceId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [ContactId] uniqueidentifier NULL,
    [LeadId] uniqueidentifier NULL,
    [RecipientEmail] nvarchar(256) NULL,
    [RecipientName] nvarchar(256) NULL,
    [CurrentStep] int NOT NULL,
    [Status] nvarchar(32) NOT NULL,
    [EnrolledAtUtc] datetime2 NOT NULL,
    [LastSentAtUtc] datetime2 NULL,
    [NextSendAtUtc] datetime2 NULL,
    [CompletedAtUtc] datetime2 NULL,
    CONSTRAINT [PK_EmailSequenceEnrollments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmailSequenceEnrollments_Contacts_ContactId] FOREIGN KEY ([ContactId]) REFERENCES [Contacts] ([Id]),
    CONSTRAINT [FK_EmailSequenceEnrollments_EmailSequences_SequenceId] FOREIGN KEY ([SequenceId]) REFERENCES [EmailSequences] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmailSequenceEnrollments_Leads_LeadId] FOREIGN KEY ([LeadId]) REFERENCES [Leads] ([Id]),
    CONSTRAINT [FK_EmailSequenceEnrollments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
);
GO

CREATE TABLE [TaskItems] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [OrganizationId] uniqueidentifier NULL,
    [Title] nvarchar(512) NOT NULL,
    [Description] nvarchar(max) NULL,
    [DueDateUtc] datetime2 NULL,
    [ReminderDateUtc] datetime2 NULL,
    [Status] nvarchar(32) NOT NULL DEFAULT N'Todo',
    [Priority] nvarchar(32) NOT NULL DEFAULT N'None',
    [Completed] bit NOT NULL,
    [AssigneeId] uniqueidentifier NULL,
    [LeadId] uniqueidentifier NULL,
    [DealId] uniqueidentifier NULL,
    [ContactId] uniqueidentifier NULL,
    [Notes] nvarchar(max) NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NULL,
    [CompletedAtUtc] datetime2 NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    CONSTRAINT [PK_TaskItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TaskItems_Contacts_ContactId] FOREIGN KEY ([ContactId]) REFERENCES [Contacts] ([Id]),
    CONSTRAINT [FK_TaskItems_Deals_DealId] FOREIGN KEY ([DealId]) REFERENCES [Deals] ([Id]),
    CONSTRAINT [FK_TaskItems_Leads_LeadId] FOREIGN KEY ([LeadId]) REFERENCES [Leads] ([Id]),
    CONSTRAINT [FK_TaskItems_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]),
    CONSTRAINT [FK_TaskItems_Users_AssigneeId] FOREIGN KEY ([AssigneeId]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_TaskItems_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_TaskItems_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_Activities_ContactId] ON [Activities] ([ContactId]);
GO

CREATE INDEX [IX_Activities_DealId] ON [Activities] ([DealId]);
GO

CREATE INDEX [IX_Activities_LeadId] ON [Activities] ([LeadId]);
GO

CREATE INDEX [IX_Activities_OrganizationId] ON [Activities] ([OrganizationId]);
GO

CREATE INDEX [IX_Activities_UpdatedByUserId] ON [Activities] ([UpdatedByUserId]);
GO

CREATE INDEX [IX_Activities_UserId] ON [Activities] ([UserId]);
GO

CREATE INDEX [IX_Companies_OrganizationId] ON [Companies] ([OrganizationId]);
GO

CREATE INDEX [IX_Companies_UpdatedByUserId] ON [Companies] ([UpdatedByUserId]);
GO

CREATE INDEX [IX_Companies_UserId] ON [Companies] ([UserId]);
GO

CREATE INDEX [IX_Contacts_ArchivedByUserId] ON [Contacts] ([ArchivedByUserId]);
GO

CREATE INDEX [IX_Contacts_CompanyId] ON [Contacts] ([CompanyId]);
GO

CREATE UNIQUE INDEX [IX_Contacts_ConvertedFromLeadId] ON [Contacts] ([ConvertedFromLeadId]) WHERE [ConvertedFromLeadId] IS NOT NULL;
GO

CREATE INDEX [IX_Contacts_DoNotContact] ON [Contacts] ([DoNotContact]);
GO

CREATE UNIQUE INDEX [IX_Contacts_Email_OrganizationId] ON [Contacts] ([Email], [OrganizationId]) WHERE [IsArchived] = 0;
GO

CREATE INDEX [IX_Contacts_IsArchived] ON [Contacts] ([IsArchived]);
GO

CREATE INDEX [IX_Contacts_OrganizationId] ON [Contacts] ([OrganizationId]);
GO

CREATE INDEX [IX_Contacts_UpdatedByUserId] ON [Contacts] ([UpdatedByUserId]);
GO

CREATE INDEX [IX_Contacts_UserId] ON [Contacts] ([UserId]);
GO

CREATE INDEX [IX_CopyHistoryItems_OrganizationId] ON [CopyHistoryItems] ([OrganizationId]);
GO

CREATE INDEX [IX_CopyHistoryItems_UserId] ON [CopyHistoryItems] ([UserId]);
GO

CREATE INDEX [IX_Deals_AssigneeId] ON [Deals] ([AssigneeId]);
GO

CREATE INDEX [IX_Deals_CompanyId] ON [Deals] ([CompanyId]);
GO

CREATE INDEX [IX_Deals_ContactId] ON [Deals] ([ContactId]);
GO

CREATE INDEX [IX_Deals_DealStageId] ON [Deals] ([DealStageId]);
GO

CREATE INDEX [IX_Deals_OrganizationId] ON [Deals] ([OrganizationId]);
GO

CREATE INDEX [IX_Deals_PipelineId] ON [Deals] ([PipelineId]);
GO

CREATE INDEX [IX_Deals_UpdatedByUserId] ON [Deals] ([UpdatedByUserId]);
GO

CREATE INDEX [IX_Deals_UserId] ON [Deals] ([UserId]);
GO

CREATE INDEX [IX_DealStages_PipelineId] ON [DealStages] ([PipelineId]);
GO

CREATE INDEX [IX_EmailSequenceEnrollments_ContactId] ON [EmailSequenceEnrollments] ([ContactId]);
GO

CREATE INDEX [IX_EmailSequenceEnrollments_LeadId] ON [EmailSequenceEnrollments] ([LeadId]);
GO

CREATE INDEX [IX_EmailSequenceEnrollments_NextSendAtUtc] ON [EmailSequenceEnrollments] ([NextSendAtUtc]);
GO

CREATE INDEX [IX_EmailSequenceEnrollments_SequenceId] ON [EmailSequenceEnrollments] ([SequenceId]);
GO

CREATE INDEX [IX_EmailSequenceEnrollments_Status] ON [EmailSequenceEnrollments] ([Status]);
GO

CREATE INDEX [IX_EmailSequenceEnrollments_UserId] ON [EmailSequenceEnrollments] ([UserId]);
GO

CREATE INDEX [IX_EmailSequences_IsActive] ON [EmailSequences] ([IsActive]);
GO

CREATE INDEX [IX_EmailSequences_OrganizationId] ON [EmailSequences] ([OrganizationId]);
GO

CREATE INDEX [IX_EmailSequences_OrganizationId_IsSharedWithOrganization] ON [EmailSequences] ([OrganizationId], [IsSharedWithOrganization]);
GO

CREATE INDEX [IX_EmailSequences_UserId] ON [EmailSequences] ([UserId]);
GO

CREATE INDEX [IX_EmailSequenceSteps_SequenceId] ON [EmailSequenceSteps] ([SequenceId]);
GO

CREATE INDEX [IX_EmailSequenceSteps_SequenceId_StepOrder] ON [EmailSequenceSteps] ([SequenceId], [StepOrder]);
GO

CREATE INDEX [IX_Invites_OrganizationId_Email] ON [Invites] ([OrganizationId], [Email]);
GO

CREATE UNIQUE INDEX [IX_Invites_Token] ON [Invites] ([Token]);
GO

CREATE INDEX [IX_JoinRequests_OrganizationId_UserId] ON [JoinRequests] ([OrganizationId], [UserId]);
GO

CREATE INDEX [IX_JoinRequests_UserId] ON [JoinRequests] ([UserId]);
GO

CREATE INDEX [IX_Leads_CompanyId] ON [Leads] ([CompanyId]);
GO

CREATE INDEX [IX_Leads_ConvertedToDealId] ON [Leads] ([ConvertedToDealId]);
GO

CREATE INDEX [IX_Leads_IsConverted] ON [Leads] ([IsConverted]);
GO

CREATE INDEX [IX_Leads_LeadSourceId] ON [Leads] ([LeadSourceId]);
GO

CREATE INDEX [IX_Leads_LeadStatusId] ON [Leads] ([LeadStatusId]);
GO

CREATE INDEX [IX_Leads_OrganizationId] ON [Leads] ([OrganizationId]);
GO

CREATE INDEX [IX_Leads_UpdatedByUserId] ON [Leads] ([UpdatedByUserId]);
GO

CREATE INDEX [IX_Leads_UserId] ON [Leads] ([UserId]);
GO

CREATE INDEX [IX_LeadSources_OrganizationId] ON [LeadSources] ([OrganizationId]);
GO

CREATE INDEX [IX_LeadStatuses_OrganizationId] ON [LeadStatuses] ([OrganizationId]);
GO

CREATE INDEX [IX_OrganizationMembers_UserId] ON [OrganizationMembers] ([UserId]);
GO

CREATE INDEX [IX_Organizations_OwnerUserId] ON [Organizations] ([OwnerUserId]);
GO

CREATE INDEX [IX_Pipelines_OrganizationId] ON [Pipelines] ([OrganizationId]);
GO

CREATE INDEX [IX_TaskItems_AssigneeId] ON [TaskItems] ([AssigneeId]);
GO

CREATE INDEX [IX_TaskItems_ContactId] ON [TaskItems] ([ContactId]);
GO

CREATE INDEX [IX_TaskItems_DealId] ON [TaskItems] ([DealId]);
GO

CREATE INDEX [IX_TaskItems_DueDateUtc] ON [TaskItems] ([DueDateUtc]);
GO

CREATE INDEX [IX_TaskItems_LeadId] ON [TaskItems] ([LeadId]);
GO

CREATE INDEX [IX_TaskItems_OrganizationId] ON [TaskItems] ([OrganizationId]);
GO

CREATE INDEX [IX_TaskItems_OrganizationId_Status] ON [TaskItems] ([OrganizationId], [Status]);
GO

CREATE INDEX [IX_TaskItems_Status] ON [TaskItems] ([Status]);
GO

CREATE INDEX [IX_TaskItems_UpdatedByUserId] ON [TaskItems] ([UpdatedByUserId]);
GO

CREATE INDEX [IX_TaskItems_UserId_Status] ON [TaskItems] ([UserId], [Status]);
GO

CREATE INDEX [IX_Templates_IsSystemTemplate] ON [Templates] ([IsSystemTemplate]);
GO

CREATE INDEX [IX_Templates_OrganizationId] ON [Templates] ([OrganizationId]);
GO

CREATE INDEX [IX_Templates_UserId] ON [Templates] ([UserId]);
GO

CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
GO

ALTER TABLE [Activities] ADD CONSTRAINT [FK_Activities_Contacts_ContactId] FOREIGN KEY ([ContactId]) REFERENCES [Contacts] ([Id]);
GO

ALTER TABLE [Activities] ADD CONSTRAINT [FK_Activities_Deals_DealId] FOREIGN KEY ([DealId]) REFERENCES [Deals] ([Id]);
GO

ALTER TABLE [Activities] ADD CONSTRAINT [FK_Activities_Leads_LeadId] FOREIGN KEY ([LeadId]) REFERENCES [Leads] ([Id]);
GO

ALTER TABLE [Contacts] ADD CONSTRAINT [FK_Contacts_Leads_ConvertedFromLeadId] FOREIGN KEY ([ConvertedFromLeadId]) REFERENCES [Leads] ([Id]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260206130556_InitialCreate', N'8.0.11');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [TaskItems] ADD [ReminderSentAtUtc] datetime2 NULL;
GO

ALTER TABLE [Deals] ADD [ClosedAtUtc] datetime2 NULL;
GO

ALTER TABLE [Deals] ADD [ClosedReason] nvarchar(500) NULL;
GO

ALTER TABLE [Deals] ADD [ClosedReasonCategory] nvarchar(50) NULL;
GO

ALTER TABLE [Deals] ADD [Description] nvarchar(2000) NULL;
GO

ALTER TABLE [Deals] ADD [Probability] int NULL;
GO

ALTER TABLE [Contacts] ADD [Description] nvarchar(max) NULL;
GO

ALTER TABLE [Companies] ADD [Description] nvarchar(2000) NULL;
GO

ALTER TABLE [Companies] ADD [Location] nvarchar(300) NULL;
GO

ALTER TABLE [Companies] ADD [Website] nvarchar(500) NULL;
GO

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
GO

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
GO

CREATE INDEX [IX_DealStageChanges_ChangedByUserId] ON [DealStageChanges] ([ChangedByUserId]);
GO

CREATE INDEX [IX_DealStageChanges_DealId] ON [DealStageChanges] ([DealId]);
GO

CREATE INDEX [IX_TaskComments_AuthorId] ON [TaskComments] ([AuthorId]);
GO

CREATE INDEX [IX_TaskComments_TaskItemId] ON [TaskComments] ([TaskItemId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260209103032_AddDealEnhancements', N'8.0.11');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Organizations] ADD [WebhookPassword] nvarchar(256) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260506124606_AddOrganizationWebhookPassword', N'8.0.11');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF COL_LENGTH('dbo.Leads', 'AssignedToUserId') IS NULL
    ALTER TABLE [Leads] ADD [AssignedToUserId] uniqueidentifier NULL;

IF COL_LENGTH('dbo.Leads', 'PipelineState') IS NULL
    ALTER TABLE [Leads] ADD [PipelineState] nvarchar(max) NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Leads_AssignedToUserId' AND object_id = OBJECT_ID('dbo.Leads'))
    CREATE INDEX [IX_Leads_AssignedToUserId] ON [Leads] ([AssignedToUserId]);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Leads_OrganizationId_CreatedAtUtc' AND object_id = OBJECT_ID('dbo.Leads'))
    CREATE INDEX [IX_Leads_OrganizationId_CreatedAtUtc] ON [Leads] ([OrganizationId], [CreatedAtUtc]);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Leads_OrganizationId_Status' AND object_id = OBJECT_ID('dbo.Leads'))
    CREATE INDEX [IX_Leads_OrganizationId_Status] ON [Leads] ([OrganizationId], [Status]);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Activities_OrganizationId_LeadId' AND object_id = OBJECT_ID('dbo.Activities'))
    CREATE INDEX [IX_Activities_OrganizationId_LeadId] ON [Activities] ([OrganizationId], [LeadId]);

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Leads_Users_AssignedToUserId')
    ALTER TABLE [Leads] ADD CONSTRAINT [FK_Leads_Users_AssignedToUserId]
        FOREIGN KEY ([AssignedToUserId]) REFERENCES [Users] ([Id]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261005124929_SpeedUpLeadAndActivityLookups', N'8.0.11');
GO

COMMIT;
GO


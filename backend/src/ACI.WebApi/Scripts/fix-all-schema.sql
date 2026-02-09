-- Comprehensive Database Schema Fix Script
-- This script applies all schema fixes including CompanyName -> BrandName migration
-- Safe to run multiple times (idempotent)

USE [ACI];
GO

PRINT 'Starting database schema fixes...';
GO

-- ============================================
-- 1. Fix UserSettings table - CompanyName to BrandName
-- ============================================
PRINT '1. Checking UserSettings table...';

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'CompanyName')
AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrandName')
BEGIN
    EXEC sp_rename 'UserSettings.CompanyName', 'BrandName', 'COLUMN';
    PRINT '   ✓ Renamed CompanyName to BrandName';
END
ELSE IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrandName')
BEGIN
    PRINT '   ✓ BrandName column already exists';
END
ELSE
BEGIN
    PRINT '   ⚠ UserSettings table or CompanyName column not found';
END
GO

-- ============================================
-- 2. Add missing UserSettings columns
-- ============================================
PRINT '2. Adding missing UserSettings columns...';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'JobTitle')
    ALTER TABLE [UserSettings] ADD [JobTitle] nvarchar(128) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'AvatarUrl')
    ALTER TABLE [UserSettings] ADD [AvatarUrl] nvarchar(512) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'Phone')
    ALTER TABLE [UserSettings] ADD [Phone] nvarchar(32) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'Timezone')
    ALTER TABLE [UserSettings] ADD [Timezone] nvarchar(64) NOT NULL DEFAULT 'UTC';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'Language')
    ALTER TABLE [UserSettings] ADD [Language] nvarchar(10) NOT NULL DEFAULT 'en';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'Bio')
    ALTER TABLE [UserSettings] ADD [Bio] nvarchar(500) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'EmailSignature')
    ALTER TABLE [UserSettings] ADD [EmailSignature] nvarchar(2000) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'DefaultEmailSubjectPrefix')
    ALTER TABLE [UserSettings] ADD [DefaultEmailSubjectPrefix] nvarchar(100) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'Theme')
    ALTER TABLE [UserSettings] ADD [Theme] int NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'DataDensity')
    ALTER TABLE [UserSettings] ADD [DataDensity] int NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'SidebarCollapsed')
    ALTER TABLE [UserSettings] ADD [SidebarCollapsed] bit NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'ShowWelcomeBanner')
    ALTER TABLE [UserSettings] ADD [ShowWelcomeBanner] bit NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'EmailNotificationsEnabled')
    ALTER TABLE [UserSettings] ADD [EmailNotificationsEnabled] bit NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'EmailOnNewLead')
    ALTER TABLE [UserSettings] ADD [EmailOnNewLead] bit NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'EmailOnDealUpdate')
    ALTER TABLE [UserSettings] ADD [EmailOnDealUpdate] bit NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'EmailOnTaskDue')
    ALTER TABLE [UserSettings] ADD [EmailOnTaskDue] bit NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'EmailOnTeamMention')
    ALTER TABLE [UserSettings] ADD [EmailOnTeamMention] bit NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'InAppNotificationsEnabled')
    ALTER TABLE [UserSettings] ADD [InAppNotificationsEnabled] bit NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'InAppSoundEnabled')
    ALTER TABLE [UserSettings] ADD [InAppSoundEnabled] bit NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrowserNotificationsEnabled')
    ALTER TABLE [UserSettings] ADD [BrowserNotificationsEnabled] bit NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'DefaultPipelineId')
    ALTER TABLE [UserSettings] ADD [DefaultPipelineId] nvarchar(64) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'DefaultLeadStatusId')
    ALTER TABLE [UserSettings] ADD [DefaultLeadStatusId] nvarchar(64) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'DefaultLeadSourceId')
    ALTER TABLE [UserSettings] ADD [DefaultLeadSourceId] nvarchar(64) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'DefaultFollowUpDays')
    ALTER TABLE [UserSettings] ADD [DefaultFollowUpDays] int NOT NULL DEFAULT 3;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'DefaultCurrency')
    ALTER TABLE [UserSettings] ADD [DefaultCurrency] nvarchar(10) NULL DEFAULT 'USD';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'ShowActivityStatus')
    ALTER TABLE [UserSettings] ADD [ShowActivityStatus] bit NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'AllowAnalytics')
    ALTER TABLE [UserSettings] ADD [AllowAnalytics] bit NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'CreatedAtUtc')
    ALTER TABLE [UserSettings] ADD [CreatedAtUtc] datetime2 NOT NULL DEFAULT GETUTCDATE();
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'UpdatedAtUtc')
    ALTER TABLE [UserSettings] ADD [UpdatedAtUtc] datetime2 NOT NULL DEFAULT GETUTCDATE();

PRINT '   ✓ UserSettings columns checked/added';
GO

-- ============================================
-- 3. Verification
-- ============================================
PRINT '3. Verifying schema...';
GO

SELECT 
    CASE 
        WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrandName')
        THEN '✓ BrandName EXISTS'
        ELSE '✗ BrandName MISSING'
    END AS BrandNameStatus,
    CASE 
        WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'CompanyName')
        THEN '✗ CompanyName STILL EXISTS'
        ELSE '✓ CompanyName REMOVED'
    END AS CompanyNameStatus;
GO

PRINT 'Database schema fixes completed!';
GO

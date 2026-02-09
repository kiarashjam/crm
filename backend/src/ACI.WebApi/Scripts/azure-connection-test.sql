-- Azure Database Connection Test and Quick Fix
-- Run this in Azure Portal Query Editor or Azure Data Studio

-- Test connection and check current schema state
PRINT '=== Azure Database Schema Check ===';
GO

-- 1. Check UserSettings BrandName migration
PRINT '';
PRINT '1. Checking UserSettings BrandName migration...';
SELECT 
    CASE 
        WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrandName')
        THEN '✓ BrandName EXISTS'
        ELSE '✗ BrandName MISSING - Migration needed'
    END AS BrandNameStatus,
    CASE 
        WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'CompanyName')
        THEN '✗ CompanyName STILL EXISTS - Migration needed'
        ELSE '✓ CompanyName REMOVED'
    END AS CompanyNameStatus;
GO

-- 2. Check if UserSettings table exists
PRINT '';
PRINT '2. Checking UserSettings table...';
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'UserSettings')
    PRINT '✓ UserSettings table exists'
ELSE
    PRINT '✗ UserSettings table MISSING - Run migrations first';
GO

-- 3. Check for other critical tables
PRINT '';
PRINT '3. Checking critical tables...';
SELECT 
    CASE WHEN EXISTS (SELECT * FROM sys.tables WHERE name = 'Organizations') THEN '✓' ELSE '✗' END AS Organizations,
    CASE WHEN EXISTS (SELECT * FROM sys.tables WHERE name = 'Pipelines') THEN '✓' ELSE '✗' END AS Pipelines,
    CASE WHEN EXISTS (SELECT * FROM sys.tables WHERE name = 'DealStages') THEN '✓' ELSE '✗' END AS DealStages,
    CASE WHEN EXISTS (SELECT * FROM sys.tables WHERE name = 'LeadSources') THEN '✓' ELSE '✗' END AS LeadSources,
    CASE WHEN EXISTS (SELECT * FROM sys.tables WHERE name = 'LeadStatuses') THEN '✓' ELSE '✗' END AS LeadStatuses;
GO

-- 4. Quick fix: Apply BrandName migration if needed
PRINT '';
PRINT '4. Applying BrandName migration if needed...';
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'CompanyName')
AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrandName')
BEGIN
    EXEC sp_rename 'UserSettings.CompanyName', 'BrandName', 'COLUMN';
    PRINT '✓ Successfully renamed CompanyName to BrandName';
END
ELSE IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrandName')
BEGIN
    PRINT '✓ BrandName already exists - migration not needed';
END
ELSE
BEGIN
    PRINT '⚠ UserSettings table or CompanyName column not found';
END
GO

-- 5. Final verification
PRINT '';
PRINT '5. Final verification...';
SELECT 
    CASE 
        WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrandName')
        THEN '✓ BrandName EXISTS'
        ELSE '✗ BrandName MISSING'
    END AS FinalStatus;
GO

PRINT '';
PRINT '=== Schema Check Complete ===';
GO

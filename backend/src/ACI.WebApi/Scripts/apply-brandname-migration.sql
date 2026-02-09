-- Apply CompanyName to BrandName Migration
-- This script renames the CompanyName column to BrandName in UserSettings table
-- Safe to run multiple times (idempotent)

-- Rename CompanyName to BrandName if column exists
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'CompanyName')
AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrandName')
BEGIN
    EXEC sp_rename 'UserSettings.CompanyName', 'BrandName', 'COLUMN';
    PRINT '✓ Successfully renamed CompanyName to BrandName';
END
ELSE IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrandName')
BEGIN
    PRINT '✓ BrandName column already exists - migration not needed';
END
ELSE
BEGIN
    PRINT '⚠ UserSettings table or CompanyName column not found';
END

-- Verify the migration
SELECT 
    CASE 
        WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrandName')
        THEN '✓ BrandName column exists'
        ELSE '✗ BrandName column MISSING'
    END AS BrandNameStatus,
    CASE 
        WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'CompanyName')
        THEN '✗ CompanyName still exists'
        ELSE '✓ CompanyName has been removed'
    END AS CompanyNameStatus;

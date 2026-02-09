-- Verify Database Schema - CompanyName to BrandName Migration
-- Run this script to verify the UserSettings table has BrandName column

-- Check if BrandName column exists
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrandName')
    PRINT '✓ BrandName column exists in UserSettings table'
ELSE
    PRINT '✗ BrandName column MISSING in UserSettings table'

-- Check if CompanyName column still exists (should not)
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'CompanyName')
    PRINT '✗ CompanyName column still exists - needs migration'
ELSE
    PRINT '✓ CompanyName column has been removed (migrated to BrandName)'

-- Show all columns in UserSettings table
SELECT 
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('UserSettings')
ORDER BY c.column_id;

-- Check for any other tables that might have CompanyName (should only be OrgSettings which is correct)
SELECT 
    t.name AS TableName,
    c.name AS ColumnName
FROM sys.tables t
INNER JOIN sys.columns c ON t.object_id = c.object_id
WHERE c.name = 'CompanyName'
ORDER BY t.name, c.name;

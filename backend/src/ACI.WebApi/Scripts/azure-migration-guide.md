# Azure Database Migration Guide

## Overview
This guide explains how to ensure the `CompanyName` → `BrandName` migration is applied to your Azure SQL Database.

## Automatic Migration
The application **automatically applies the migration** when it starts. The migration script in `Program.cs` (lines 424-428) checks for the `CompanyName` column and renames it to `BrandName` if needed.

## Manual Verification (Optional)

### Option 1: Using Azure Portal
1. Go to Azure Portal → Your App Service → Configuration → Connection Strings
2. Copy the connection string for `DefaultConnection`
3. Use Azure Data Studio or SQL Server Management Studio to connect
4. Run the verification script: `Scripts/verify-database-schema.sql`

### Option 2: Using Azure CLI
```bash
# Connect to Azure SQL Database
az sql db show-connection-string --client ado.net --server <your-server> --name <your-db>

# Then connect using sqlcmd or Azure Data Studio
```

### Option 3: Application Startup Logs
When the application starts in Azure, check the logs for:
- `"Checking and fixing database schema..."`
- The migration script will run automatically and log success/failure

## Verification Query
Run this query to verify the migration:

```sql
-- Check if BrandName exists
SELECT 
    CASE 
        WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrandName')
        THEN '✓ BrandName column exists'
        ELSE '✗ BrandName column MISSING'
    END AS Status;

-- Check if CompanyName still exists (should return 0 rows)
SELECT 
    CASE 
        WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'CompanyName')
        THEN '✗ CompanyName still exists - migration needed'
        ELSE '✓ CompanyName has been migrated'
    END AS Status;
```

## Troubleshooting

### If migration fails:
1. Check Azure App Service logs for errors
2. Verify connection string is correct in Azure Portal
3. Ensure the database user has ALTER TABLE permissions
4. Run the migration script manually if needed (see `Program.cs` lines 424-428)

### Manual Migration Script
If automatic migration fails, run this SQL script manually:

```sql
-- Rename CompanyName to BrandName
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'CompanyName')
AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrandName')
    EXEC sp_rename 'UserSettings.CompanyName', 'BrandName', 'COLUMN';
```

## Notes
- The migration is **idempotent** - it's safe to run multiple times
- The script checks if `CompanyName` exists before renaming
- The script checks if `BrandName` already exists to avoid errors
- No data loss - this is a column rename operation

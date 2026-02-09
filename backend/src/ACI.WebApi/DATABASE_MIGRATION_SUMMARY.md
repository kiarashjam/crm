# Database Migration Summary - CompanyName → BrandName

## ✅ Local Database - COMPLETED

**Status:** Migration applied successfully
- ✓ `BrandName` column exists in `UserSettings` table
- ✓ `CompanyName` column has been removed

**Migration Applied:** February 9, 2026

## 🔄 Azure Database - AUTOMATIC MIGRATION

### How It Works

The migration is **automatically applied** when your Azure App Service starts. The migration script is embedded in `Program.cs` (lines 424-428) and runs on every application startup.

### Migration Script (from Program.cs)

```sql
-- Rename CompanyName to BrandName if column exists
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'CompanyName')
AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrandName')
    EXEC sp_rename 'UserSettings.CompanyName', 'BrandName', 'COLUMN';
```

### Verification Steps for Azure

1. **Deploy the updated application** to Azure App Service
2. **Restart the App Service** to trigger the migration script
3. **Check Application Logs** for:
   - `"Checking and fixing database schema..."`
   - Any errors related to the migration

4. **Verify Migration** (optional - using Azure Portal):
   - Go to Azure Portal → SQL Databases → Your Database → Query Editor
   - Run verification query:
   ```sql
   SELECT 
       CASE 
           WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrandName')
           THEN '✓ BrandName EXISTS'
           ELSE '✗ BrandName MISSING'
       END AS Status;
   ```

### Manual Migration (if automatic fails)

If the automatic migration doesn't work, you can run the manual script:

1. Connect to your Azure SQL Database using Azure Portal Query Editor or SQL Server Management Studio
2. Run the script: `Scripts/apply-brandname-migration.sql`

### Important Notes

- ✅ **Idempotent**: The migration script is safe to run multiple times
- ✅ **No Data Loss**: This is a column rename operation - all data is preserved
- ✅ **Automatic**: The migration runs automatically on app startup
- ✅ **Backward Compatible**: The code checks for both column names during transition

### Troubleshooting

**If migration fails:**
1. Check Azure App Service logs for detailed error messages
2. Verify database connection string is correct in App Service Configuration
3. Ensure the database user has `ALTER TABLE` permissions
4. Run the manual migration script if needed

**Connection String Location:**
- Azure Portal → App Service → Configuration → Connection Strings → `DefaultConnection`

## Files Created

1. `Scripts/verify-database-schema.sql` - Verification script
2. `Scripts/apply-brandname-migration.sql` - Manual migration script
3. `Scripts/azure-migration-guide.md` - Detailed Azure migration guide

## Next Steps

1. ✅ Local database migration complete
2. 🔄 Deploy updated code to Azure
3. 🔄 Restart Azure App Service (migration runs automatically)
4. ✅ Verify migration in Azure logs or database

---

**Migration Date:** February 9, 2026  
**Status:** Local ✅ | Azure 🔄 (Pending deployment)

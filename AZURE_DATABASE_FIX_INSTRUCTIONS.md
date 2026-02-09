# Azure Database Fix Instructions

## ✅ Quick Fix: Automatic (Recommended)

The application **automatically fixes the database** when it starts in Azure. Just follow these steps:

### Step 1: Deploy Updated Code
Deploy the latest code to your Azure App Service (the code with all the migration scripts).

### Step 2: Restart App Service
1. Go to Azure Portal
2. Navigate to your App Service
3. Click **"Restart"** button
4. Wait for restart to complete (~30 seconds)

### Step 3: Verify Migration
Check the application logs:
1. Azure Portal → App Service → **Log stream**
2. Look for these log messages:
   - `"Checking and fixing database schema..."`
   - `"Schema check complete"`
   - `"Applying database migrations..."`

**That's it!** The migration runs automatically. No manual SQL needed.

---

## 🔍 Manual Verification (Optional)

If you want to verify the migration manually:

### Option A: Azure Portal Query Editor

1. **Azure Portal** → **SQL Databases** → Your Database → **Query Editor**
2. **Login** with your database credentials
3. **Run this query**:
   ```sql
   SELECT 
       CASE 
           WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrandName')
           THEN '✓ BrandName EXISTS'
           ELSE '✗ BrandName MISSING'
       END AS Status;
   ```

### Option B: Azure Data Studio / SSMS

1. Connect to your Azure SQL Database
2. Run the verification script: `backend/src/ACI.WebApi/Scripts/azure-connection-test.sql`

---

## 🛠️ Manual Fix (If Automatic Fails)

If the automatic migration doesn't work, run this manually:

### Step 1: Connect to Azure SQL Database
- Use Azure Portal Query Editor, Azure Data Studio, or SQL Server Management Studio

### Step 2: Run Migration Script
Run this SQL script:

```sql
-- Rename CompanyName to BrandName
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'CompanyName')
AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserSettings') AND name = 'BrandName')
BEGIN
    EXEC sp_rename 'UserSettings.CompanyName', 'BrandName', 'COLUMN';
    PRINT '✓ Successfully renamed CompanyName to BrandName';
END
```

Or use the comprehensive script: `backend/src/ACI.WebApi/Scripts/fix-all-schema.sql`

---

## 📋 What Gets Fixed Automatically

When the application starts, it automatically:

1. ✅ **Renames** `CompanyName` → `BrandName` in UserSettings
2. ✅ **Adds** all missing columns to UserSettings (JobTitle, AvatarUrl, Phone, etc.)
3. ✅ **Creates** missing tables (Organizations, Pipelines, DealStages, etc.)
4. ✅ **Adds** missing columns to Leads, Deals, Companies, Contacts
5. ✅ **Fixes** all schema inconsistencies

All fixes are **idempotent** - safe to run multiple times.

---

## 🔧 Troubleshooting

### Issue: Application won't start
- Check Azure App Service logs for errors
- Verify connection string is correct in App Service Configuration
- Ensure database is accessible from App Service

### Issue: Migration not running
- Check application logs for: `"Checking and fixing database schema..."`
- Verify the app is actually starting (check health endpoint)
- Try restarting the App Service again

### Issue: Permission errors
- Ensure database user has `ALTER TABLE` permissions
- Check connection string uses correct credentials
- Verify firewall rules allow App Service IP

### Issue: Connection timeout
- Check firewall rules in Azure SQL Database
- Verify "Allow Azure services" is enabled
- Check connection string format

---

## 📝 Connection String Location

**Azure Portal** → **App Service** → **Configuration** → **Connection Strings** → `DefaultConnection`

The connection string should look like:
```
Server=tcp:your-server.database.windows.net,1433;Initial Catalog=your-db;Persist Security Info=False;User ID=your-user;Password=your-password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

---

## ✅ Verification Checklist

After deploying and restarting:

- [ ] Application starts without errors
- [ ] Logs show "Checking and fixing database schema..."
- [ ] Logs show "Schema check complete"
- [ ] BrandName column exists (verify with query)
- [ ] CompanyName column removed (verify with query)
- [ ] Application functions correctly

---

## 📄 Files Available

1. **`Scripts/azure-connection-test.sql`** - Quick verification and fix script
2. **`Scripts/fix-all-schema.sql`** - Comprehensive manual fix script
3. **`Scripts/apply-brandname-migration.sql`** - Just BrandName migration
4. **`Scripts/verify-database-schema.sql`** - Detailed verification queries

---

**Important Notes:**
- ✅ Migrations are **automatic** - just restart the app
- ✅ Migrations are **idempotent** - safe to run multiple times
- ✅ **No data loss** - column renames preserve all data
- ✅ **Backward compatible** - checks for both column names

---

**Date:** February 9, 2026  
**Status:** Ready for Azure - Automatic migration on app restart

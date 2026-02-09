# Azure Database Fix Guide

## Overview

The application automatically applies all database schema fixes when it starts in Azure. This guide explains how to ensure the fixes are applied to your Azure SQL Database.

## Automatic Migration

**The application automatically fixes the database on startup!**

When your Azure App Service starts, the code in `Program.cs` (lines 232-850+) automatically:
1. Checks for missing tables and creates them
2. Checks for missing columns and adds them
3. Renames `CompanyName` to `BrandName` if needed
4. Applies all schema fixes (idempotent - safe to run multiple times)

## Steps to Fix Azure Database

### Option 1: Automatic (Recommended)

1. **Deploy the updated code** to Azure App Service
2. **Restart the App Service**:
   - Azure Portal → Your App Service → Overview → Restart
   - Or use Azure CLI: `az webapp restart --name <app-name> --resource-group <resource-group>`
3. **Check Application Logs**:
   - Azure Portal → App Service → Log stream
   - Look for: `"Checking and fixing database schema..."`
   - Look for: `"Schema check complete"`
   - Look for: `"Applying database migrations..."`

The migration runs automatically - no manual SQL needed!

### Option 2: Manual Verification (Optional)

If you want to verify the migration manually:

1. **Connect to Azure SQL Database**:
   - Azure Portal → SQL Databases → Your Database → Query Editor
   - Or use Azure Data Studio / SQL Server Management Studio

2. **Run Verification Query**:
   ```sql
   -- Check BrandName migration
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
   ```

3. **If Migration Not Applied**, run manual script:
   - Use the script: `Scripts/fix-all-schema.sql`
   - Or the specific migration: `Scripts/apply-brandname-migration.sql`

## Connection String Configuration

The application reads the connection string from Azure App Service Configuration:

**Location:** Azure Portal → App Service → Configuration → Connection Strings → `DefaultConnection`

**Format:** Should be a SQL Server connection string pointing to your Azure SQL Database.

## What Gets Fixed Automatically

### 1. UserSettings Table
- ✅ `CompanyName` → `BrandName` migration
- ✅ All missing columns added (JobTitle, AvatarUrl, Phone, Timezone, etc.)
- ✅ Notification settings columns
- ✅ Default settings columns

### 2. Other Tables
- ✅ Organizations, OrganizationMembers, Invites, JoinRequests
- ✅ Pipelines, DealStages, LeadSources, LeadStatuses
- ✅ Leads, Deals, Companies, Contacts enhancements
- ✅ Templates enhancements

## Troubleshooting

### If automatic migration doesn't work:

1. **Check Application Logs**:
   - Look for errors related to database connection or schema fixes
   - Check for permission errors

2. **Verify Connection String**:
   - Ensure `DefaultConnection` is set correctly
   - Test connection from Azure Portal Query Editor

3. **Check Database Permissions**:
   - The database user needs `ALTER TABLE` permissions
   - Verify the connection string uses a user with sufficient permissions

4. **Run Manual Script**:
   - Connect to Azure SQL Database
   - Run `Scripts/fix-all-schema.sql` manually

### Common Issues

**Issue:** "Login failed for user"
- **Solution:** Check connection string credentials in App Service Configuration

**Issue:** "ALTER TABLE permission denied"
- **Solution:** Ensure database user has `ALTER TABLE` permissions

**Issue:** Migration not running
- **Solution:** Check application logs for errors, verify app is starting correctly

## Verification Checklist

After deploying and restarting:

- [ ] Application starts without errors
- [ ] Logs show "Checking and fixing database schema..."
- [ ] Logs show "Schema check complete"
- [ ] BrandName column exists in UserSettings
- [ ] CompanyName column removed from UserSettings
- [ ] All tables exist (Organizations, Pipelines, etc.)
- [ ] Application functions correctly

## Manual Migration Scripts

If you need to run migrations manually:

1. **`Scripts/fix-all-schema.sql`** - Comprehensive fix for all tables
2. **`Scripts/apply-brandname-migration.sql`** - Just the BrandName migration
3. **`Scripts/verify-database-schema.sql`** - Verification queries

## Notes

- ✅ All migrations are **idempotent** - safe to run multiple times
- ✅ **No data loss** - column renames preserve all data
- ✅ **Automatic** - runs on every app startup until complete
- ✅ **Backward compatible** - checks for both column names during transition

---

**Date:** February 9, 2026  
**Status:** Ready for Azure deployment

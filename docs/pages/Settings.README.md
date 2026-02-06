# Settings

**Route:** `/settings`  
**File:** `Settings.tsx`

## Purpose

Comprehensive user settings management with tabbed interface for profile, brand, appearance, notifications, organization, pipelines, security, and account settings.

## Features

### Tab Navigation (Sidebar)
8 organized sections with URL-synced tabs (`?tab=profile`):

1. **Profile** - Personal info, avatar, job title, phone, timezone, language, bio
2. **Brand & AI** - Brand tone (Professional/Friendly/Persuasive), email signature, subject prefix
3. **Appearance** - Theme (Light/Dark/System), data density, sidebar collapse, welcome banner
4. **Notifications** - Email notifications (new lead, deal update, task due, team mention), digest frequency, in-app sounds, browser notifications
5. **Team** - Organization switching, invites, join requests, member management (for owners)
6. **Pipelines** - Pipeline and stage CRUD (for owners)
7. **Security** - Two-factor authentication setup/disable
8. **Account** - Default currency, follow-up days, privacy toggles, data export, reset settings, logout, delete account

## Behavior

- On load: `getUserSettings()`, `getTimezones()`, `getCurrencies()`, `getLanguages()`, `twoFactorSetup()`
- Change tracking with "Save Changes" button (enabled when `hasChanges` is true)
- Partial updates - only modified fields sent to backend
- Toast notifications on save/error
- URL-synced tabs for bookmarking

## API / Data

### Settings
- **GET:** `getUserSettings()` - Fetch all user settings
- **PUT:** `saveUserSettings(settings)` - Partial update settings
- **PATCH:** `saveSettingsSection(section, settings)` - Update specific section
- **POST:** `resetUserSettings()` - Reset to defaults

### Reference Data
- **GET:** `getTimezones()` - Available timezones
- **GET:** `getCurrencies()` - Available currencies
- **GET:** `getLanguages()` - Available languages

### Organization (for owners)
- `createInvite()`, `listPendingInvitesForOrg()`, `listPendingJoinRequestsForOrg()`
- `acceptJoinRequest()`, `rejectJoinRequest()`
- `getOrgMembers()`, `updateMemberRole()`, `removeMember()`

### Pipelines (for owners)
- `getPipelines()`, `createPipeline()`, `updatePipeline()`, `deletePipeline()`
- `getDealStagesByPipeline()`, `createDealStage()`, `updateDealStage()`, `deleteDealStage()`

### 2FA
- `twoFactorSetup()`, `twoFactorEnable()`, `twoFactorDisable()`

## Settings Schema

```typescript
interface UserSettings {
  // Profile
  companyName: string;
  jobTitle?: string;
  avatarUrl?: string;
  phone?: string;
  timezone: string;
  language: string;
  bio?: string;

  // Brand
  brandTone: 'professional' | 'friendly' | 'persuasive';
  emailSignature?: string;
  defaultEmailSubjectPrefix?: string;

  // Appearance
  theme: 'light' | 'dark' | 'system';
  dataDensity: 'comfortable' | 'compact' | 'spacious';
  sidebarCollapsed: boolean;
  showWelcomeBanner: boolean;

  // Notifications
  emailNotificationsEnabled: boolean;
  emailOnNewLead: boolean;
  emailOnDealUpdate: boolean;
  emailOnTaskDue: boolean;
  emailOnTeamMention: boolean;
  emailDigestFrequency: 'never' | 'daily' | 'weekly' | 'monthly';
  inAppNotificationsEnabled: boolean;
  inAppSoundEnabled: boolean;
  browserNotificationsEnabled: boolean;

  // Defaults
  defaultPipelineId?: string;
  defaultLeadStatusId?: string;
  defaultLeadSourceId?: string;
  defaultFollowUpDays: number;
  defaultCurrency?: string;

  // Privacy
  showActivityStatus: boolean;
  allowAnalytics: boolean;
}
```

# Frontend Code Quality, Performance & Standards Improvement Report

> **Generated**: February 2026  
> **Last Updated**: February 6, 2026 - Major large file refactoring completed  
> **Scope**: `src/` directory - React/TypeScript frontend codebase  
> **Total Files Analyzed**: 119 TypeScript/TSX files  
> **Total Lines of Code**: ~25,000+ lines in pages alone  
> **Refactoring Progress**: 6 of 11 large files completed with significant reductions  
> **Industry Research**: Based on 2025-2026 best practices from React, TypeScript, and TanStack Query documentation  
> **Related Report**: See [BACKEND_CODE_QUALITY_AND_STANDARDS_REPORT.md](./BACKEND_CODE_QUALITY_AND_STANDARDS_REPORT.md) for backend improvements

---

## 🎉 Implementation Status

This section tracks what has been implemented from the recommendations in this report.

### ✅ Completed Implementations

| Category | Item | Status | Location |
|----------|------|--------|----------|
| **Dependencies** | TanStack Query | ✅ Installed | `package.json` |
| **Dependencies** | TanStack Virtual | ✅ Installed | `package.json` |
| **Dependencies** | Zod (validation) | ✅ Installed | `package.json` |
| **Dependencies** | react-error-boundary | ✅ Installed | `package.json` |
| **Dependencies** | ESLint + Prettier | ✅ Installed | `package.json` devDependencies |
| **Dependencies** | Vitest + Testing Library | ✅ Installed | `package.json` devDependencies |
| **Dependencies** | MSW (Mock Service Worker) | ✅ Installed | `package.json` devDependencies |
| **Config** | `.eslintrc.cjs` | ✅ Created | Project root |
| **Config** | `.prettierrc` | ✅ Created | Project root |
| **Config** | `.editorconfig` | ✅ Created | Project root |
| **Config** | `vitest.config.ts` | ✅ Created | Project root |
| **Config** | TypeScript stricter options | ✅ Updated | `tsconfig.json` |
| **Scripts** | lint, format, test commands | ✅ Added | `package.json` |
| **Performance** | Code Splitting (Lazy Loading) | ✅ Implemented | `src/app/App.tsx` |
| **Performance** | QueryProvider setup | ✅ Created | `src/app/providers/QueryProvider.tsx` |
| **Architecture** | Query Keys factory | ✅ Created | `src/app/hooks/queries/queryKeys.ts` |
| **Architecture** | TanStack Query hooks | ✅ Created | `src/app/hooks/queries/*.ts` (7 files) |
| **Architecture** | Utility hooks extracted | ✅ Created | `src/app/hooks/*.ts` (5 hooks) |
| **Architecture** | Configuration files | ✅ Created | `src/app/config/*.ts` (5 files) |
| **Architecture** | Date utilities | ✅ Created | `src/app/utils/dateFormatters.ts` |
| **Components** | ErrorBoundary | ✅ Created | `src/app/components/ErrorBoundary.tsx` |
| **Components** | PageLoader | ✅ Created | `src/app/components/PageLoader.tsx` |
| **Testing** | Test setup with MSW | ✅ Created | `src/test/setup.ts` |
| **Testing** | MSW handlers | ✅ Created | `src/test/mocks/handlers.ts` |
| **Testing** | Test utilities | ✅ Created | `src/test/utils.tsx` |
| **Testing** | Sample tests | ✅ Created | 4 test files (27 tests passing) |
| **Testing** | EmptyState tests | ✅ Created | `src/app/components/EmptyState.test.tsx` |
| **Testing** | ErrorBoundary tests | ✅ Created | `src/app/components/ErrorBoundary.test.tsx` |
| **Bug Fixes** | JSX tag mismatches | ✅ Fixed | 6 page files (see below) |
| **Bug Fixes** | TypeScript errors | ✅ Fixed | All ~100+ errors resolved |
| **Refactoring** | Dashboard.tsx split | ✅ Extracted | 6 components to `pages/dashboard/` (41% reduction) |
| **Refactoring** | Homepage.tsx split | ✅ Extracted | Animations to `pages/homepage/animations.tsx` (23% reduction) |
| **Refactoring** | Leads.tsx split | ✅ Extracted | LeadDetailModal, ConvertLeadDialog, AddLeadDialog to `pages/leads/` (50% reduction) |
| **Refactoring** | Settings.tsx split | ✅ Extracted | 8 section components to `pages/settings/components/` (65% reduction) |
| **Refactoring** | Pipeline.tsx split | ✅ Extracted | DealCard, DroppableStageColumn to `pages/pipeline/` (19% reduction) |
| **Refactoring** | Tasks.tsx split | ✅ Extracted | KanbanTaskCard, KanbanColumn, ListTaskCard, TaskGroupSection to `pages/tasks/components/` (24% reduction) |
| **Refactoring** | Page folder structures | ✅ Created | 11 page subdirectories with types/config files |

### 🐛 Bug Fixes Applied

The following JSX tag mismatch errors were fixed to allow the build to pass:

| File | Issue | Fix Applied |
|------|-------|-------------|
| `Pipeline.tsx` | Missing `</div>` in search/filter bar | Added closing tag for flex row container |
| `Team.tsx` | Missing `</div>` in role filter section | Added closing tag for filter wrapper |
| `Contacts.tsx` | Missing `</div>` in sort dropdown | Added closing tag for sort wrapper |
| `Companies.tsx` | Missing `</div>` in sort dropdown | Added closing tag for sort wrapper |
| `Activities.tsx` | Missing `</div>` in type filter | Added closing tag for filter wrapper |
| `Leads.tsx` | Missing `</div>` in sort dropdown | Added closing tag for sort wrapper |

All 6 files had a common pattern where a `<div className="flex gap-3">` or `<div className="relative group/...">` wrapper was opened inside the search/filter bar section but never closed before the parent container closed.

### 🔧 TypeScript Fixes Applied (February 5, 2026)

All ~100+ TypeScript errors were systematically fixed across the codebase:

| Category | Files Fixed | Changes Made |
|----------|-------------|--------------|
| **Unused Imports** | 15+ files | Removed unused lucide-react icons, components, and utilities |
| **Unused Variables** | 10+ files | Prefixed with `_` or removed completely |
| **Possibly Undefined** | 12+ files | Added optional chaining (`?.`), nullish coalescing (`??`), and null checks |
| **Type Mismatches** | 8+ files | Added type assertions, explicit types to useState, type conversions |
| **Property Errors** | 5+ files | Updated component logic to match existing interfaces |
| **Duplicate Exports** | 2 files | Consolidated `WebhookInfo` type to single location |

**Key Files Modified**:
- `Activities.tsx` - Removed unused imports
- `Companies.tsx` - Fixed property access, removed unused imports
- `Contacts.tsx` - Fixed property access, removed unused imports  
- `Dashboard.tsx` - Added explicit types, non-null assertions
- `Homepage.tsx` - Added optional chaining, removed unused components
- `Leads.tsx` - Major fixes for type aliases, conversions, null checks
- `LeadImport.tsx` - Added null checks for parsed data
- `LeadWebhook.tsx` - Fixed import path for WebhookInfo
- `Pipeline.tsx` - Added optional chaining, explicit types
- `Settings.tsx` - Removed unused imports, prefixed variables
- `Tasks.tsx` - Removed unused imports
- `Team.tsx` - Added non-null assertions
- `Organizations.tsx` - Removed unused imports
- `AppHeader.tsx` - Added explicit type to useState
- `OrgContext.tsx` - Added optional chaining
- `src/app/api/*.ts` - Fixed type conversions, removed unused imports

### 📁 New Directory Structure

```
src/
├── app/
│   ├── config/                    # ✅ NEW - Extracted configurations
│   │   ├── activityTypes.ts       # Activity type definitions
│   │   ├── leadConfig.ts          # Lead status colors, source icons
│   │   ├── navigation.ts          # Navigation items
│   │   ├── taskConfig.ts          # Task status/priority configs
│   │   └── index.ts
│   ├── hooks/                     # ✅ NEW - Extracted & query hooks
│   │   ├── queries/               # TanStack Query hooks
│   │   │   ├── queryKeys.ts       # Centralized query keys
│   │   │   ├── useLeads.ts
│   │   │   ├── useCompanies.ts
│   │   │   ├── useContacts.ts
│   │   │   ├── useDeals.ts
│   │   │   ├── useTasks.ts
│   │   │   ├── useActivities.ts
│   │   │   ├── useTemplates.ts
│   │   │   └── index.ts
│   │   ├── useInView.ts           # Intersection observer hook
│   │   ├── useMousePosition.ts    # Mouse tracking hook
│   │   ├── useParallax.ts         # Parallax scrolling hook
│   │   ├── useDebounce.ts         # Debounce hook
│   │   ├── useLocalStorage.ts     # localStorage persistence
│   │   └── index.ts
│   ├── providers/                 # ✅ NEW - React providers
│   │   ├── QueryProvider.tsx      # TanStack Query setup
│   │   └── index.ts
│   ├── utils/                     # ✅ NEW - Utility functions
│   │   ├── dateFormatters.ts      # Date formatting utilities
│   │   └── index.ts
│   └── components/
│       ├── ErrorBoundary.tsx      # ✅ NEW - Error boundary
│       └── PageLoader.tsx         # ✅ NEW - Loading states
├── test/                          # ✅ NEW - Testing infrastructure
│   ├── mocks/
│   │   ├── handlers.ts            # MSW request handlers
│   │   └── server.ts              # MSW server setup
│   ├── setup.ts                   # Test setup
│   └── utils.tsx                  # Custom render with providers
```

### 📊 Build & Test Status

| Check | Status | Command | Notes |
|-------|--------|---------|-------|
| Build | ✅ Passing | `npm run build` | All JSX errors fixed |
| TypeScript | ✅ 0 Errors | `npm run typecheck` | All errors fixed |
| Tests | ✅ 27/27 Passing | `npm run test` | 4 test files |
| Lint | ✅ Available | `npm run lint` | ESLint configured |
| Format | ✅ Available | `npm run format` | Prettier configured |

### 🔄 Remaining Tasks (Not Yet Implemented)

| Category | Item | Priority | Notes |
|----------|------|----------|-------|
| ~~**Refactoring**~~ | ~~Split `Leads.tsx`~~ | ~~P0~~ | ✅ 50% reduction (3,522 → 1,748 lines) |
| ~~**Refactoring**~~ | ~~Split `Homepage.tsx`~~ | ~~P0~~ | ✅ 23% reduction (2,498 → 1,912 lines) |
| ~~**Refactoring**~~ | ~~Split `Pipeline.tsx`~~ | ~~P0~~ | ✅ 19% reduction (2,188 → 1,766 lines) |
| ~~**Refactoring**~~ | ~~Split `Settings.tsx`~~ | ~~P1~~ | ✅ 65% reduction (2,044 → 713 lines) |
| ~~**Refactoring**~~ | ~~Split `Tasks.tsx`~~ | ~~P1~~ | ✅ 24% reduction (1,725 → 1,318 lines) |
| **Refactoring** | Split remaining large files | P2 | Team.tsx, Companies.tsx, Contacts.tsx, Activities.tsx |
| **Migration** | Use TanStack Query in pages | P1 | Replace useState/useEffect patterns |
| **Migration** | Use extracted configs in pages | P2 | Import from `@/app/config` |
| **Migration** | Use date utilities in pages | P2 | Import from `@/app/utils` |
| ~~**Cleanup**~~ | ~~Remove `http.ts` duplicate~~ | ~~P2~~ | ✅ Deprecated with docs |
| ~~**Cleanup**~~ | ~~Fix pre-existing TS errors~~ | ~~P2~~ | ✅ All 100+ errors fixed |
| ~~**Testing**~~ | ~~Add more component tests~~ | ~~P3~~ | ✅ 4 test files, 27 tests |
| **Performance** | Add virtual scrolling to lists | P3 | For large data tables |

---

## Industry Standards & Best Practices (2025-2026)

This section summarizes current industry best practices that inform the recommendations in this report.

### React 19 + TypeScript Standards

According to the [React TypeScript Style Guide](https://react-typescript-style-guide.com/) and industry experts:

| Principle | Description | Your Codebase Status |
|-----------|-------------|---------------------|
| **Single Responsibility** | Components should have one reason to change | ❌ Large pages mix concerns |
| **Minimal Mental Overhead** | Code should be easy to scan without excessive context | ❌ 3,600+ line files |
| **Early Returns** | Use early returns to reduce nesting | ⚠️ Partial |
| **Separation of Concerns** | Logic, UI, and state should be separated | ❌ Mixed in pages |
| **No Unnecessary Abstraction** | Avoid over-engineering | ✅ Good |

**Key Industry Statistic**: Teams adopting automated code quality checks (ESLint + Prettier) report a **40% decrease in bugs** and **25% reduction in bugs during development**.

### Feature-Based Architecture (2025 Standard)

The industry has moved away from flat structures (`components/`, `pages/`, `hooks/`) toward **feature-based organization**:

```
src/
├── features/
│   ├── auth/
│   │   ├── components/
│   │   ├── hooks/
│   │   ├── services/
│   │   └── types.ts
│   ├── leads/
│   ├── deals/
│   └── pipeline/
├── shared/
│   ├── components/
│   ├── hooks/
│   └── utils/
└── app/
    └── routes/
```

**Benefits documented in industry**:
- **Reduced coupling**: UI changes don't require touching data-fetching logic
- **Improved cohesion**: Files that change together live together
- **Better scalability**: Structure grows with project instead of fighting complexity
- **Clear boundaries**: Explicit separation between generic and feature-specific code

### TanStack Query (2025 Data Fetching Standard)

Manual `useState` + `useEffect` patterns are now considered an **anti-pattern**. TanStack Query has become the industry standard because:

| Manual Approach Problems | TanStack Query Solution |
|--------------------------|------------------------|
| Race conditions | Automatic request cancellation |
| Stale data issues | Intelligent caching & background refetch |
| Boilerplate code (100+ lines) | 5-10 lines per query |
| No request deduplication | Automatic deduplication |
| Manual loading states | Built-in loading/error states |
| No optimistic updates | Built-in optimistic update support |

**Industry Pattern - Query Options Reuse**:
```typescript
// Create reusable query options (2025 best practice)
export const leadsQueryOptions = {
  queryKey: ['leads'],
  queryFn: fetchLeads,
  staleTime: 5 * 60 * 1000,
};

// Reuse across components
const { data } = useQuery(leadsQueryOptions);
```

### Code Splitting & Lazy Loading (2025 Performance Standard)

According to [React documentation](https://reactjs.org/docs/code-splitting.html) and performance experts:

**Route-Based Splitting** is the easiest and most effective approach:
- Users don't visit every page in one session
- Split by routes with `React.lazy()` + `Suspense`
- Include related utilities, hooks, and styles with route modules

**Common Mistakes to Avoid**:
- Too many small chunks (network overhead outweighs benefits)
- Not aligning chunks with actual user navigation patterns
- Forgetting to include related code in chunks (waterfall loading)

**Performance Impact**: Proper code splitting reduces **Largest Contentful Paint (LCP)** and **First Input Delay (FID)**.

### Component Design Patterns (2025)

**Compound Components** are now the standard for flexible UIs (used by Radix UI, shadcn/ui):

```typescript
// Industry standard pattern (what you're already using with shadcn/ui)
<Dialog>
  <DialogTrigger>Open</DialogTrigger>
  <DialogContent>
    <DialogHeader>
      <DialogTitle>Title</DialogTitle>
    </DialogHeader>
  </DialogContent>
</Dialog>
```

**When to use Compound Components**:
- Maximum UI flexibility needed
- Avoid prop complexity ("prop soup")
- Components should "just work" together like native HTML

### Error Handling (2025 Best Practices)

**Layered Error Boundaries** are the standard:
1. **Global boundary** - Catches app-wide crashes
2. **Route boundary** - Isolates route-level errors
3. **Feature boundary** - Granular error isolation

**Important**: Error Boundaries only catch rendering errors. For async operations:
- Wrap in try-catch blocks
- Use Promise.catch()
- Handle errors in useEffect with try-catch

### Testing Standards (2025)

**Vitest + React Testing Library** is the current industry standard:

| Why Vitest Over Jest | Benefit |
|---------------------|---------|
| Vite-native | Seamless integration |
| Faster | Parallel execution, native ESM |
| TypeScript support | No extra config |
| Jest-compatible API | Easy migration |

**Five Essential Testing Areas**:
1. **Queries** - Find elements the way users do
2. **User Events** - Interact as users would
3. **Network Mocking** - Use MSW for API requests
4. **Element Presence** - Test what appears AND what doesn't
5. **Page Navigation** - Test routing

---

## Executive Summary

Your frontend codebase uses modern technologies (React 18, TypeScript, Vite, Tailwind CSS, shadcn/ui) but has significant opportunities for improvement in:

1. **Code Organization** - Large monolithic components need splitting (7 pages over 1,000 lines)
2. **Performance** - Missing data fetching optimization, no lazy loading, no virtualization
3. **Code Quality Tooling** - No ESLint/Prettier configured
4. **State Management** - 37+ useEffect hooks for data fetching, 11+ manual loading states
5. **TypeScript Strictness** - `noUnusedLocals` and `noUnusedParameters` disabled
6. **Anti-patterns** - Hooks defined inside components, inline configurations, prop drilling

---

## Quick Stats (Code Analysis Results)

| Metric | Count | Files Affected | Impact |
|--------|-------|----------------|--------|
| useEffect for data fetching | 37+ | All pages | High - migrate to TanStack Query |
| Manual `setLoading(true)` calls | 14 | 10 pages | High - eliminatable boilerplate |
| `Promise.all()` in useEffect | 6 | Companies, Contacts, Activities, Team | Medium - consolidate |
| Inline config objects | 23+ | Leads, Tasks, Pipeline, Activities | High - extract to config files |
| Components defined inside pages | 8 | Leads, Team, Homepage, Pipeline | Critical - extract immediately |
| Hooks defined inside pages | 4 | Homepage, AppHeader | Critical - extract to hooks/ |
| Magic strings (status/types) | 50+ | All pages | Medium - create enums |
| Silent error catch blocks | 12 | API calls | High - add error logging |
| Mock data mixed with production | 3 | Team, Leads, Pipeline | Medium - remove or conditionalize |
| Try/catch error handlers | 28 | Medium - inconsistent error handling |
| Pages over 1,000 lines | 7 | Critical - maintainability issues |
| Pages over 500 lines | 12 | High - need refactoring |
| Inline hook definitions | 3+ | Medium - should extract to hooks folder |
| Inline config objects | 15+ | Medium - should extract to constants |

---

## Table of Contents

**🎉 [Implementation Status](#-implementation-status)** - What's been completed

1. [Critical Issues](#1-critical-issues)
   - Oversized Page Components
   - ~~Missing Code Quality Tools~~ ✅ IMPLEMENTED
   - ~~No Code Splitting in Router~~ ✅ IMPLEMENTED
   - Duplicate HTTP Client Implementations (deprecated)
2. [Specific Anti-Patterns Found](#2-specific-anti-patterns-found)
   - Hooks Defined Inside Page Components
   - Large Inline Configuration Objects
   - Repeated Data Fetching Pattern
   - Inconsistent Error Handling
   - Deeply Nested Component Trees
   - Missing Memoization
   - Magic Strings Throughout Codebase
3. [Performance Optimizations](#3-performance-optimizations)
   - Add TanStack Query
   - Implement Code Splitting with Lazy Loading
   - Optimize Bundle Size
   - Add Virtual Scrolling for Large Lists
4. [Code Quality & Standards](#4-code-quality--standards)
   - Create Custom Hooks for Reusable Logic
   - Establish Component Patterns
   - Add JSDoc Comments
   - Error Boundary Implementation
5. [Code Organization & Architecture](#5-code-organization--architecture)
   - Recommended Folder Structure
   - Feature-Based Module Pattern
   - Constants & Configuration
6. [TypeScript Improvements](#6-typescript-improvements)
   - Enable Stricter Compiler Options
   - Use Discriminated Unions for State
   - Create Strict API Types
   - Use Zod for Runtime Validation
7. [API Layer Improvements](#7-api-layer-improvements)
   - Consolidate HTTP Clients
   - Add Request Interceptors
   - Add Retry Logic
   - Remove Mock Data Layer
8. [CSS & Styling Improvements](#8-css--styling-improvements)
   - Current State Analysis
   - Issues Found
   - CSS Best Practices
9. [Implementation Roadmap](#9-implementation-roadmap)
   - Phase 1: Quick Wins
   - Phase 2: Data Layer
   - Phase 3: Performance
   - Phase 4: Refactoring
   - Phase 5: Testing
10. [File-by-File Analysis](#10-file-by-file-analysis)
    - Page Components Analysis
    - API Layer Analysis
    - Component Analysis
    - Context & Utility Analysis
11. [Metrics to Track](#11-metrics-to-track)
    - Key Performance Indicators

**Appendices**
- [Appendix A: Sample Configuration Files](#appendix-a-sample-configuration-files)
- [Appendix B: Recommended Dependencies](#appendix-b-recommended-dependencies)
- [Appendix C: Testing Setup](#appendix-c-testing-setup-2025-industry-standard)
- [Appendix D: Refactoring Checklist](#appendix-d-refactoring-checklist)
- [Appendix E: Quick Commands Reference](#appendix-e-quick-commands-reference)

---

## 1. Critical Issues

### 1.1 Oversized Page Components ✅ MAJOR PROGRESS

**Problem**: Several page components are excessively large, making them hard to maintain, test, and reason about.

**Current Status (February 6, 2026)**:
- ✅ Folder structures created for all large files
- ✅ **6 major files refactored** with significant line reductions
- ✅ Components extracted to dedicated folders with proper types, configs, and utils
- ✅ TypeScript compilation passing with 0 errors

| File | Original Lines | Current Lines | Reduction | Status |
|------|----------------|---------------|-----------|--------|
| `Leads.tsx` | 3,522 | 1,748 | **50%** | ✅ LeadDetailModal, ConvertLeadDialog, AddLeadDialog extracted |
| `Settings.tsx` | 2,044 | 713 | **65%** | ✅ 8 section components extracted (Profile, Brand, Appearance, Notifications, Organization, Pipelines, Security, Account) |
| `Homepage.tsx` | 2,498 | 1,912 | **23%** | ✅ Animations extracted |
| `Pipeline.tsx` | 2,188 | 1,766 | **19%** | ✅ DealCard, DroppableStageColumn, utils extracted |
| `Tasks.tsx` | 1,725 | 1,318 | **24%** | ✅ KanbanTaskCard, KanbanColumn, ListTaskCard, TaskGroupSection extracted |
| `Dashboard.tsx` | 870 | 511 | **41%** | ✅ Components extracted |
| `Team.tsx` | 1,127 | 1,079 | 4% | 🔄 Folder created |
| `Companies.tsx` | 1,120 | 1,074 | 4% | 🔄 Folder created |
| `Contacts.tsx` | 1,089 | 1,049 | 4% | 🔄 Folder created |
| `Activities.tsx` | 1,006 | 959 | 5% | 🔄 Folder created |
| `LeadImport.tsx` | 805 | 775 | 4% | 🔄 Folder created |

**Total Lines Reduced**: ~3,717 lines across 6 major files

**Best Practice**: Components should ideally be under 300-400 lines. Over 500 lines indicates need for splitting.

**New Directory Structure Created**:
```
pages/
├── leads/
│   ├── types.ts           # ✅ Lead-related type definitions
│   ├── config.ts          # ✅ Status colors, source icons, constants
│   ├── utils.ts           # ✅ Utility functions (formatDate, getInitials, etc.)
│   ├── AddLeadDialog.tsx  # ✅ Extracted dialog component
│   ├── LeadCard.tsx       # ✅ Lead card component
│   ├── LeadStats.tsx      # ✅ Statistics display
│   ├── LeadFilters.tsx    # ✅ Filter controls
│   └── index.ts           # ✅ Barrel exports
├── dashboard/
│   ├── types.ts           # ✅ Dashboard type definitions
│   ├── config.ts          # ✅ Copy types, goals, nav items
│   ├── DashboardHero.tsx  # ✅ Hero section with stats
│   ├── PipelineChart.tsx  # ✅ Pipeline visualization
│   ├── QuickNav.tsx       # ✅ Quick navigation links
│   ├── CopyStatsWidget.tsx # ✅ Writer stats widget
│   ├── RecentActivity.tsx # ✅ Activity feed
│   ├── TeamPerformance.tsx # ✅ Team metrics
│   └── index.ts           # ✅ Barrel exports
├── homepage/
│   ├── types.ts           # ✅ Feature, Testimonial, PricingTier types
│   ├── config.ts          # ✅ Features, pricing tiers, hero stats
│   ├── animations.tsx     # ✅ All animation hooks and components
│   └── index.ts           # ✅ Barrel exports
├── pipeline/              # ✅ Folder with types.ts, config.ts
├── settings/              # ✅ Folder with types.ts, config.ts
├── tasks/                 # ✅ Folder with types.ts, config.ts
├── team/                  # ✅ Folder with types.ts, config.ts
├── companies/             # ✅ Folder with types.ts, config.ts
├── contacts/              # ✅ Folder with types.ts, config.ts
├── activities/            # ✅ Folder with types.ts, config.ts, utils.ts
└── lead-import/           # ✅ Folder with types.ts, config.tsx
```

**Next Steps for Further Reduction**:
1. Extract remaining components from Leads.tsx (largest file)
2. Extract Settings tabs into individual components
3. Extract Pipeline board/list views
4. Extract Tasks board/list views

### 1.2 Missing Code Quality Tools ✅ IMPLEMENTED

> **Status**: ✅ Completed on February 5, 2026

**Problem**: No ESLint or Prettier configuration found.

**Impact**:
- Inconsistent code formatting across files
- Potential bugs not caught at compile time
- No enforcement of best practices

**✅ What Was Implemented**:
- `.eslintrc.cjs` - ESLint configuration with TypeScript and React rules
- `.prettierrc` - Prettier formatting configuration
- `.editorconfig` - Editor settings for consistent coding
- `vitest.config.ts` - Test configuration
- Package.json scripts: `lint`, `lint:fix`, `format`, `format:check`, `test`, `typecheck`

**Solution**: Add these configurations:

**`.eslintrc.cjs`**:
```javascript
module.exports = {
  root: true,
  env: { browser: true, es2020: true },
  extends: [
    'eslint:recommended',
    'plugin:@typescript-eslint/recommended',
    'plugin:@typescript-eslint/recommended-type-checked',
    'plugin:react/recommended',
    'plugin:react/jsx-runtime',
    'plugin:react-hooks/recommended',
    'prettier'
  ],
  ignorePatterns: ['dist', '.eslintrc.cjs'],
  parser: '@typescript-eslint/parser',
  parserOptions: {
    ecmaVersion: 'latest',
    sourceType: 'module',
    project: ['./tsconfig.json'],
    tsconfigRootDir: __dirname,
  },
  plugins: ['react-refresh', '@typescript-eslint'],
  settings: {
    react: { version: '18.3' }
  },
  rules: {
    'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
    '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
    '@typescript-eslint/no-explicit-any': 'error',
    'react-hooks/exhaustive-deps': 'warn',
    'react-hooks/rules-of-hooks': 'error',
  },
};
```

**`.prettierrc`**:
```json
{
  "semi": true,
  "singleQuote": true,
  "tabWidth": 2,
  "trailingComma": "es5",
  "printWidth": 100
}
```

**Add to `package.json`**:
```json
{
  "scripts": {
    "lint": "eslint src --ext ts,tsx --report-unused-disable-directives --max-warnings 0",
    "lint:fix": "eslint src --ext ts,tsx --fix",
    "format": "prettier --write \"src/**/*.{ts,tsx,css,json}\""
  },
  "devDependencies": {
    "@typescript-eslint/eslint-plugin": "^7.0.0",
    "@typescript-eslint/parser": "^7.0.0",
    "eslint": "^8.57.0",
    "eslint-config-prettier": "^9.1.0",
    "eslint-plugin-react": "^7.34.0",
    "eslint-plugin-react-hooks": "^4.6.0",
    "eslint-plugin-react-refresh": "^0.4.5",
    "prettier": "^3.2.0"
  }
}
```

### 1.3 No Code Splitting in Router ✅ IMPLEMENTED

> **Status**: ✅ Completed on February 5, 2026

**Problem**: All 25 page components were eagerly loaded in `App.tsx`.

**Previous** (`App.tsx`):
```typescript
// All imports were synchronous - loaded on initial page load
import Homepage from '@/app/pages/Homepage';        // 2,498 lines
import Leads from '@/app/pages/Leads';              // 3,676 lines
import Pipeline from '@/app/pages/Pipeline';        // 2,252 lines
// ... 20 more imports
```

**✅ What Was Implemented** (`src/app/App.tsx`):
```typescript
// Eager-loaded pages (critical path)
import Homepage from '@/app/pages/Homepage';
import Login from '@/app/pages/Login';

// Lazy-loaded pages (code splitting)
const Onboarding = lazy(() => import('@/app/pages/Onboarding'));
const Dashboard = lazy(() => import('@/app/pages/Dashboard'));
const Leads = lazy(() => import('@/app/pages/Leads'));
const Pipeline = lazy(() => import('@/app/pages/Pipeline'));
// ... all other pages lazy loaded
```

**Benefits Achieved**:
- ✅ 20+ routes now lazy-loaded
- ✅ Initial bundle significantly smaller
- ✅ Suspense with PageLoader fallback
- ✅ ErrorBoundary wrapping entire app

### 1.4 Duplicate HTTP Client Implementations ⚠️ DEPRECATED

> **Status**: ⚠️ `http.ts` marked as deprecated, use `apiClient.ts` for new code

**Problem**: Two HTTP client implementations exist:
- `src/app/api/apiClient.ts` - Uses raw fetch (PREFERRED)
- `src/app/api/http.ts` - Provides axios-style wrapper (DEPRECATED)

**✅ Action Taken**:
- Added deprecation notice to `http.ts`
- Documented that new code should use `authFetchJson` from `apiClient.ts`
- Existing code using `http.ts` continues to work (backwards compatible)

**Impact**:
- Confusion about which to use
- Inconsistent error handling
- Maintenance burden

**Solution**: Consolidate into a single client:

```typescript
// src/app/api/client.ts - Unified API client
import { getAuthToken, getCurrentOrganizationId, clearSession } from '@/app/lib/auth';

const API_BASE = import.meta.env.VITE_API_URL?.replace(/\/$/, '') ?? '';

class ApiClient {
  private baseUrl: string;

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl;
  }

  private async request<T>(path: string, init?: RequestInit): Promise<T> {
    const headers = new Headers(init?.headers);
    
    const token = getAuthToken();
    if (token) headers.set('Authorization', `Bearer ${token}`);
    
    const orgId = getCurrentOrganizationId();
    if (orgId) headers.set('X-Organization-Id', orgId);
    
    if (init?.body && !headers.has('Content-Type')) {
      headers.set('Content-Type', 'application/json');
    }

    const url = `${this.baseUrl}${path}`;
    const response = await fetch(url, { ...init, headers });

    if (response.status === 401) {
      clearSession();
      throw new Error('Unauthorized');
    }

    if (!response.ok) {
      const message = await response.text().catch(() => response.statusText);
      throw new Error(message || `HTTP ${response.status}`);
    }

    const text = await response.text();
    return text ? JSON.parse(text) : null;
  }

  get<T>(path: string) {
    return this.request<T>(path, { method: 'GET' });
  }

  post<T>(path: string, body?: unknown) {
    return this.request<T>(path, { 
      method: 'POST', 
      body: body ? JSON.stringify(body) : undefined 
    });
  }

  put<T>(path: string, body?: unknown) {
    return this.request<T>(path, { 
      method: 'PUT', 
      body: body ? JSON.stringify(body) : undefined 
    });
  }

  delete<T>(path: string) {
    return this.request<T>(path, { method: 'DELETE' });
  }
}

export const api = new ApiClient(API_BASE);
```

---

## 2. Specific Anti-Patterns Found

### 2.1 Identical Data Fetching Pattern (Found in 8 Files)

**Problem**: The exact same data fetching boilerplate is copy-pasted across pages.

**Files Affected**: `Companies.tsx`, `Contacts.tsx`, `Activities.tsx`, `Team.tsx`, `Templates.tsx`, `Tasks.tsx`, `Pipeline.tsx`, `Leads.tsx`

```typescript
// This exact pattern appears in Companies.tsx (lines 93-107), 
// Contacts.tsx (lines 70-83), Activities.tsx (lines 139-153)
useEffect(() => {
  let cancelled = false;
  setLoading(true);
  Promise.all([getCompanies(), getContacts(), getDeals()])
    .then(([companiesData, contactsData, dealsData]) => { 
      if (!cancelled) {
        setCompanies(companiesData);
        setContacts(contactsData);
        setDeals(dealsData);
      }
    })
    .catch(() => {})  // ❌ Silent fail - no error handling
    .finally(() => { if (!cancelled) setLoading(false); });
  return () => { cancelled = true; };
}, []);
```

**Issues**:
1. Race condition handling via manual `cancelled` flag
2. Silent `.catch(() => {})` hides errors
3. No retry logic
4. No caching - refetches on every mount
5. ~15 lines of boilerplate per page

**Solution with TanStack Query** (3 lines):
```typescript
const { data: companies = [], isLoading } = useQuery({
  queryKey: ['companies'],
  queryFn: getCompanies
});
```

---

### 2.2 Hooks Defined Inside Page Components

**Problem**: Custom hooks are defined inside page files instead of being extracted.

**Found in `Homepage.tsx` (lines 55-100)**:
```typescript
// These hooks are defined INSIDE the file, not reusable
function useInView(options?: IntersectionObserverInit) {
  const ref = useRef<HTMLDivElement>(null);
  const [isInView, setIsInView] = useState(false);
  // ... implementation
}

function useMousePosition() {
  const [position, setPosition] = useState({ x: 0, y: 0 });
  // ... implementation
}

function useParallax(speed = 0.5) {
  const [offset, setOffset] = useState(0);
  // ... implementation
}
```

**Issues**:
- Cannot be reused in other components
- Makes the file harder to test
- Increases file size unnecessarily

**Solution**: Extract to `src/app/hooks/`:
```typescript
// src/app/hooks/useInView.ts
export function useInView(options?: IntersectionObserverInit) { ... }

// src/app/hooks/useMousePosition.ts
export function useMousePosition() { ... }

// src/app/hooks/useParallax.ts
export function useParallax(speed = 0.5) { ... }
```

### 2.2 Large Inline Configuration Objects

**Problem**: Configuration objects are defined inline, making components harder to read.

**Found in `Tasks.tsx` (lines 120-191)**:
```typescript
// 70+ lines of configuration objects INSIDE the component file
const statusConfig: Record<TaskStatusType, { label: string; color: string; ... }> = {
  todo: { label: 'To Do', color: 'text-slate-600', ... },
  in_progress: { label: 'In Progress', color: 'text-blue-600', ... },
  // ...
};

const priorityConfig: Record<TaskPriorityType, { ... }> = { ... };
const groupConfig: Record<TaskGroup, { ... }> = { ... };
const kanbanColumns: { ... }[] = [ ... ];
```

**Found in `Pipeline.tsx` (lines 82-98)**:
```typescript
const STAGE_COLORS: Record<string, { bar: string; accent: string; ... }> = {
  Qualification: { bar: '#3b82f6', accent: 'text-blue-600', ... },
  // ...
};
const STAGE_ICONS: Record<string, typeof Briefcase> = { ... };
```

**Found in `Leads.tsx` (lines 66-95)**:
```typescript
const WIZARD_STEPS: { id: WizardStep; label: string; ... }[] = [ ... ];
```

**Solution**: Extract to configuration files:
```typescript
// src/app/config/tasks.config.ts
export const STATUS_CONFIG = { ... };
export const PRIORITY_CONFIG = { ... };
export const KANBAN_COLUMNS = [ ... ];

// src/app/config/pipeline.config.ts
export const STAGE_COLORS = { ... };
export const STAGE_ICONS = { ... };
```

### 2.3 Repeated Data Fetching Pattern

**Problem**: Every page repeats the same data fetching boilerplate.

**Pattern found in 19 files**:
```typescript
// Found in Leads.tsx, Pipeline.tsx, Tasks.tsx, Companies.tsx, etc.
const [data, setData] = useState<DataType[]>([]);
const [loading, setLoading] = useState(true);

useEffect(() => {
  async function fetchData() {
    try {
      setLoading(true);
      const result = await getDataFromApi();
      setData(result);
    } catch (error) {
      toast.error('Failed to load data');
    } finally {
      setLoading(false);
    }
  }
  fetchData();
}, []);
```

**Impact**:
- 100+ lines of boilerplate across codebase
- No request deduplication (same API called multiple times)
- No caching (data refetched on every navigation)
- No background refetching
- Inconsistent error handling

**Solution**: Use TanStack Query (see section 3.1).

### 2.4 Inconsistent Error Handling

**Problem**: 28+ try/catch blocks with inconsistent error handling.

**Variations found**:
```typescript
// Variation 1: Toast only (most common)
} catch (e) {
  toast.error('Failed to load');
}

// Variation 2: Console + toast
} catch (e) {
  console.error(e);
  toast.error(messages.error);
}

// Variation 3: Silent fail
} catch {
  // Nothing
}

// Variation 4: State-based error
} catch (e) {
  setError(e.message);
}
```

**Solution**: Centralized error handling:
```typescript
// src/app/lib/errorHandler.ts
export function handleApiError(error: unknown, context?: string) {
  const message = error instanceof Error ? error.message : 'An error occurred';
  
  // Log to error tracking (Sentry, etc.)
  if (import.meta.env.PROD) {
    // errorTracker.capture(error, { context });
  }
  
  // Show user-friendly toast
  toast.error(message);
  
  // Return for optional local handling
  return message;
}

// Usage becomes consistent:
} catch (error) {
  handleApiError(error, 'Loading leads');
}
```

### 2.5 Deeply Nested Component Trees

**Problem**: Components are not extracted, leading to deeply nested JSX.

**Example from `Leads.tsx` - AddLeadDialog component (lines 98-600+)**:
```typescript
function AddLeadDialog({
  open, onOpenChange, editingLead, form, setForm, companies, 
  sourceOptions, statusOptions, onSubmit, saving,
}: {
  // 10 props!
}) {
  const [activeStep, setActiveStep] = useState<WizardStep>('contact');
  
  // 50+ lines of internal logic
  
  return (
    <Dialog>
      <DialogContent>
        {/* 400+ lines of nested JSX */}
      </DialogContent>
    </Dialog>
  );
}
```

**Issues**:
- 10 props passed down (prop drilling)
- Component defined inside parent file
- Cannot be tested independently
- Hard to reason about

**Solution**: Extract to separate file with proper composition:
```typescript
// src/app/features/leads/components/AddLeadDialog/index.tsx
export function AddLeadDialog({ leadId, onSuccess }: AddLeadDialogProps) {
  // Use hooks for data, not props
  const { data: companies } = useCompanies();
  const { data: sources } = useLeadSources();
  const { data: statuses } = useLeadStatuses();
  const createLead = useCreateLead();
  
  return (
    <Dialog>
      <WizardSteps activeStep={activeStep}>
        <ContactStep />
        <CompanyStep />
        <QualificationStep />
        <NotesStep />
      </WizardSteps>
    </Dialog>
  );
}
```

### 2.6 Missing Memoization

**Problem**: Expensive computations and callbacks not memoized.

**Example from `Pipeline.tsx` (lines 100-141)**:
```typescript
// This function is called on every render
function groupDealsByStage(deals: Deal[], stageList: { ... }[]) {
  // Complex grouping logic
  const byId = new Map<string, Deal[]>();
  for (const s of stageList) { byId.set(s.id, []); }
  for (const deal of deals) {
    // ... complex logic
  }
  return stageList.map(s => ({ ... }));
}
```

**Solution**: Use `useMemo` for expensive computations:
```typescript
const groupedDeals = useMemo(
  () => groupDealsByStage(deals, stageList),
  [deals, stageList]
);
```

### 2.7 Magic Strings Throughout Codebase

**Problem**: Status values, routes, and keys are hardcoded strings.

**Examples**:
```typescript
// In Pipeline.tsx
const FALLBACK_STAGES = ['Qualification', 'Proposal', 'Negotiation', 'Closed Won', 'Closed Lost'];

// In Tasks.tsx
type TaskGroup = 'overdue' | 'today' | 'tomorrow' | 'thisWeek' | 'later' | 'noDue' | 'completed';

// In Leads.tsx
sourceIcons: Record<string, string> = {
  website: '🌐',
  referral: '🤝',
  // ...
};
```

**Solution**: Centralize in constants:
```typescript
// src/app/lib/constants.ts
export const DEAL_STAGES = {
  QUALIFICATION: 'Qualification',
  PROPOSAL: 'Proposal',
  NEGOTIATION: 'Negotiation',
  CLOSED_WON: 'Closed Won',
  CLOSED_LOST: 'Closed Lost',
} as const;

export type DealStage = typeof DEAL_STAGES[keyof typeof DEAL_STAGES];
```

---

## 3. Performance Optimizations

### 3.1 Add Data Fetching Library (TanStack Query) ✅ INFRASTRUCTURE READY

> **Status**: ✅ TanStack Query installed and hooks created. Pages can now migrate from useState/useEffect.

> **Industry Standard**: TanStack Query is now the de facto standard for data fetching in React applications (2025). Manual `useState` + `useEffect` patterns are considered an anti-pattern.

**✅ What Was Implemented**:
- `@tanstack/react-query` installed
- `QueryProvider` created at `src/app/providers/QueryProvider.tsx`
- Query keys factory at `src/app/hooks/queries/queryKeys.ts`
- 7 query hook files created:
  - `useLeads.ts` - useLeads, useCreateLead, useUpdateLead, useDeleteLead
  - `useCompanies.ts` - useCompanies, useCreateCompany, useUpdateCompany, useDeleteCompany
  - `useContacts.ts` - useContacts, useCreateContact, useUpdateContact, useDeleteContact
  - `useDeals.ts` - useDeals, useCreateDeal, useUpdateDeal, useDeleteDeal
  - `useTasks.ts` - useTasks, useTaskStats, useCreateTask, useUpdateTask, useDeleteTask
  - `useActivities.ts` - useActivities, useCreateActivity, useDeleteActivity
  - `useTemplates.ts` - useTemplates, useCreateTemplate, useUpdateTemplate, useDeleteTemplate

**🔄 Next Step**: Migrate pages to use these hooks instead of manual useState/useEffect.

**Problem**: Manual loading states, no caching, no request deduplication.

**Current Pattern** (anti-pattern - found 37 times in your codebase):
```typescript
// Every page does this manually
const [leads, setLeads] = useState<Lead[]>([]);
const [loading, setLoading] = useState(true);
const [error, setError] = useState<string | null>(null);

useEffect(() => {
  async function fetchLeads() {
    try {
      setLoading(true);
      const data = await getLeads();
      setLeads(data);
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }
  fetchLeads();
}, []);
```

**Industry Best Practice - TanStack Query Implementation**:

#### Step 1: Setup QueryClient Provider

```typescript
// src/app/providers/QueryProvider.tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { ReactNode, useState } from 'react';

export function QueryProvider({ children }: { children: ReactNode }) {
  // Create client inside component to avoid sharing state between requests
  const [queryClient] = useState(() => new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 5 * 60 * 1000, // 5 minutes
        gcTime: 10 * 60 * 1000,   // 10 minutes (formerly cacheTime)
        retry: 1,
        refetchOnWindowFocus: false,
      },
    },
  }));

  return (
    <QueryClientProvider client={queryClient}>
      {children}
      {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
    </QueryClientProvider>
  );
}
```

#### Step 2: Create Reusable Query Options (2025 Best Practice)

```typescript
// src/app/hooks/api/queryOptions.ts
import { queryOptions } from '@tanstack/react-query';
import { getLeads, getDeals, getTasks, getContacts, getCompanies } from '@/app/api';

// Single source of truth for query configuration
export const leadsQueryOptions = queryOptions({
  queryKey: ['leads'],
  queryFn: getLeads,
  staleTime: 5 * 60 * 1000,
});

export const dealsQueryOptions = queryOptions({
  queryKey: ['deals'],
  queryFn: getDeals,
  staleTime: 5 * 60 * 1000,
});

export const tasksQueryOptions = queryOptions({
  queryKey: ['tasks'],
  queryFn: getTasks,
  staleTime: 2 * 60 * 1000, // Tasks change more frequently
});

export const contactsQueryOptions = queryOptions({
  queryKey: ['contacts'],
  queryFn: getContacts,
});

export const companiesQueryOptions = queryOptions({
  queryKey: ['companies'],
  queryFn: getCompanies,
});
```

#### Step 3: Create Domain Hooks with Mutations

```typescript
// src/app/hooks/api/useLeads.ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { leadsQueryOptions } from './queryOptions';
import { createLead, updateLead, deleteLead } from '@/app/api';
import { toast } from 'sonner';

export function useLeads() {
  return useQuery(leadsQueryOptions);
}

export function useLead(id: string) {
  const { data: leads } = useLeads();
  return leads?.find(lead => lead.id === id);
}

export function useCreateLead() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: createLead,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['leads'] });
      toast.success('Lead created successfully');
    },
    onError: (error) => {
      toast.error(error.message || 'Failed to create lead');
    },
  });
}

export function useUpdateLead() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: Partial<Lead> }) => 
      updateLead(id, data),
    // Optimistic update (2025 best practice)
    onMutate: async ({ id, data }) => {
      await queryClient.cancelQueries({ queryKey: ['leads'] });
      const previousLeads = queryClient.getQueryData(['leads']);
      
      queryClient.setQueryData(['leads'], (old: Lead[] | undefined) =>
        old?.map(lead => lead.id === id ? { ...lead, ...data } : lead)
      );
      
      return { previousLeads };
    },
    onError: (err, variables, context) => {
      queryClient.setQueryData(['leads'], context?.previousLeads);
      toast.error('Failed to update lead');
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ['leads'] });
    },
  });
}

export function useDeleteLead() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: deleteLead,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['leads'] });
      toast.success('Lead deleted');
    },
  });
}
```

#### Step 4: Use in Components (Clean, Simple)

```typescript
// In component - BEFORE (37 lines of boilerplate)
function LeadsPage() {
  const [leads, setLeads] = useState<Lead[]>([]);
  const [loading, setLoading] = useState(true);
  // ... 35 more lines of useEffect, error handling, etc.
}

// In component - AFTER (3 lines)
function LeadsPage() {
  const { data: leads = [], isLoading, error } = useLeads();
  const createLead = useCreateLead();
  const updateLead = useUpdateLead();
  
  // That's it! Caching, deduplication, optimistic updates all handled
}
```

**Benefits**:
- Automatic caching & background refetching
- Request deduplication
- Optimistic updates
- Better loading/error states
- DevTools for debugging

**Installation**:
```bash
npm install @tanstack/react-query
```

### 2.2 Implement Code Splitting with Lazy Loading

**Problem**: All pages loaded upfront, slowing initial load.

**Solution**: Use React.lazy() for route-based code splitting:

```typescript
// src/app/App.tsx
import { lazy, Suspense } from 'react';
import LoadingSpinner from '@/app/components/LoadingSpinner';

// Lazy load heavy pages
const Leads = lazy(() => import('@/app/pages/Leads'));
const Pipeline = lazy(() => import('@/app/pages/Pipeline'));
const Dashboard = lazy(() => import('@/app/pages/Dashboard'));
const Settings = lazy(() => import('@/app/pages/Settings'));
const Homepage = lazy(() => import('@/app/pages/Homepage'));

// Wrapper component for lazy routes
function LazyPage({ children }: { children: React.ReactNode }) {
  return (
    <Suspense fallback={<LoadingSpinner centered />}>
      {children}
    </Suspense>
  );
}

// In routes:
<Route path="/leads" element={<LazyPage><Leads /></LazyPage>} />
<Route path="/pipeline" element={<LazyPage><Pipeline /></LazyPage>} />
```

### 2.3 Optimize Bundle Size

**Problem**: Large dependency tree, potential unused imports.

**Analysis Tools to Add**:
```bash
npm install -D vite-plugin-visualizer
```

```typescript
// vite.config.ts
import { visualizer } from 'vite-plugin-visualizer';

export default defineConfig({
  plugins: [
    react(),
    visualizer({
      open: true,
      filename: 'dist/stats.html',
      gzipSize: true,
    }),
  ],
});
```

**Optimizations**:

1. **Tree-shake Lucide icons** (currently importing 40+ icons):
```typescript
// Instead of:
import { Search, Plus, Pencil, Trash2, Building2... } from 'lucide-react';

// Use specific imports:
import Search from 'lucide-react/icons/search';
import Plus from 'lucide-react/icons/plus';
```

2. **Split MUI and Radix** - You have both `@mui/material` and `@radix-ui` which is redundant. Consider standardizing on one.

3. **Lazy load charts** - Recharts is large, load only when needed:
```typescript
const Chart = lazy(() => import('recharts').then(m => ({ default: m.LineChart })));
```

### 2.4 Add Virtual Scrolling for Large Lists

**Problem**: Lists with 100+ items render all DOM elements.

**Solution**: Use `@tanstack/react-virtual` for large tables:

```typescript
import { useVirtualizer } from '@tanstack/react-virtual';

function LeadsList({ leads }: { leads: Lead[] }) {
  const parentRef = useRef<HTMLDivElement>(null);
  
  const virtualizer = useVirtualizer({
    count: leads.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => 48, // Row height
    overscan: 5,
  });

  return (
    <div ref={parentRef} className="h-[600px] overflow-auto">
      <div style={{ height: virtualizer.getTotalSize() }}>
        {virtualizer.getVirtualItems().map((virtualRow) => (
          <LeadRow key={leads[virtualRow.index].id} lead={leads[virtualRow.index]} />
        ))}
      </div>
    </div>
  );
}
```

---

## 4. Code Quality & Standards

### 3.1 Create Custom Hooks for Reusable Logic

**Problem**: Business logic mixed into page components.

**Solution**: Extract into custom hooks:

```typescript
// src/app/hooks/useDebounce.ts
export function useDebounce<T>(value: T, delay: number): T {
  const [debouncedValue, setDebouncedValue] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedValue(value), delay);
    return () => clearTimeout(timer);
  }, [value, delay]);

  return debouncedValue;
}

// src/app/hooks/useLocalStorage.ts
export function useLocalStorage<T>(key: string, initialValue: T) {
  const [storedValue, setStoredValue] = useState<T>(() => {
    try {
      const item = localStorage.getItem(key);
      return item ? JSON.parse(item) : initialValue;
    } catch {
      return initialValue;
    }
  });

  const setValue = useCallback((value: T | ((val: T) => T)) => {
    const valueToStore = value instanceof Function ? value(storedValue) : value;
    setStoredValue(valueToStore);
    localStorage.setItem(key, JSON.stringify(valueToStore));
  }, [key, storedValue]);

  return [storedValue, setValue] as const;
}

// src/app/hooks/useAsyncAction.ts
export function useAsyncAction<T extends (...args: any[]) => Promise<any>>(action: T) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  const execute = useCallback(async (...args: Parameters<T>) => {
    try {
      setLoading(true);
      setError(null);
      return await action(...args);
    } catch (e) {
      setError(e instanceof Error ? e : new Error(String(e)));
      throw e;
    } finally {
      setLoading(false);
    }
  }, [action]);

  return { execute, loading, error };
}
```

### 3.2 Establish Component Patterns

**Standard Component Structure**:

```typescript
// src/app/components/LeadCard/LeadCard.tsx
import { memo } from 'react';
import type { Lead } from '@/app/api/types';
import { cn } from '@/app/components/ui/utils';
import styles from './LeadCard.module.css'; // Optional CSS modules

// Props interface at top
interface LeadCardProps {
  lead: Lead;
  onEdit?: (lead: Lead) => void;
  onDelete?: (id: string) => void;
  className?: string;
}

// Component with memo for performance
export const LeadCard = memo(function LeadCard({
  lead,
  onEdit,
  onDelete,
  className,
}: LeadCardProps) {
  // Hooks at top
  const navigate = useNavigate();

  // Event handlers
  const handleEdit = useCallback(() => {
    onEdit?.(lead);
  }, [lead, onEdit]);

  // Derived state with useMemo for expensive calculations
  const statusColor = useMemo(() => getStatusColor(lead.status), [lead.status]);

  // JSX
  return (
    <div className={cn('rounded-lg border p-4', className)}>
      {/* ... */}
    </div>
  );
});

// Default export for lazy loading compatibility
export default LeadCard;
```

### 3.3 Add JSDoc Comments for Complex Functions

```typescript
/**
 * Converts a lead to a contact, company, and/or deal.
 * 
 * @param leadId - The ID of the lead to convert
 * @param options - Conversion options
 * @param options.createContact - Whether to create a new contact
 * @param options.createDeal - Whether to create a new deal
 * @param options.existingCompanyId - ID of existing company to link
 * @returns The IDs of created/linked entities
 * @throws {Error} If the lead is already converted
 * 
 * @example
 * ```ts
 * const result = await convertLead('lead-123', {
 *   createContact: true,
 *   createDeal: true,
 *   dealName: 'New Deal',
 * });
 * ```
 */
export async function convertLead(
  leadId: string,
  options: ConvertLeadRequest
): Promise<ConvertLeadResult> {
  // ...
}
```

### 4.4 Error Boundary Implementation (2025 Best Practice)

> **Industry Standard**: Use layered error boundaries at Global → Route → Feature levels for granular error isolation. Note that Error Boundaries only catch rendering errors - async errors need try-catch.

#### Option A: Class-based (Traditional)

```typescript
// src/app/components/ErrorBoundary.tsx
import { Component, type ReactNode } from 'react';
import { Button } from '@/app/components/ui/button';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
  onReset?: () => void;
}

interface State {
  hasError: boolean;
  error?: Error;
}

export class ErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
    // Log to error tracking service (Sentry, etc.)
    console.error('Error caught by boundary:', error, errorInfo);
    
    // In production, send to error tracking
    if (import.meta.env.PROD) {
      // errorTrackingService.capture(error, { extra: errorInfo });
    }
  }

  handleReset = () => {
    this.props.onReset?.();
    this.setState({ hasError: false, error: undefined });
  };

  render() {
    if (this.state.hasError) {
      return this.props.fallback ?? (
        <div className="flex flex-col items-center justify-center p-8">
          <h2 className="text-xl font-semibold mb-2">Something went wrong</h2>
          <p className="text-muted-foreground mb-4">
            {this.state.error?.message || 'An unexpected error occurred'}
          </p>
          <Button onClick={this.handleReset}>
            Try Again
          </Button>
        </div>
      );
    }

    return this.props.children;
  }
}
```

#### Option B: react-error-boundary Library (Recommended for 2025)

```bash
npm install react-error-boundary
```

```typescript
// src/app/components/ErrorBoundary.tsx
import { ErrorBoundary as ReactErrorBoundary } from 'react-error-boundary';
import { Button } from '@/app/components/ui/button';

function ErrorFallback({ error, resetErrorBoundary }: { 
  error: Error; 
  resetErrorBoundary: () => void;
}) {
  return (
    <div className="flex flex-col items-center justify-center p-8 min-h-[200px]">
      <div className="text-destructive mb-2">
        <AlertCircle className="h-12 w-12" />
      </div>
      <h2 className="text-xl font-semibold mb-2">Something went wrong</h2>
      <p className="text-muted-foreground mb-4 text-center max-w-md">
        {error.message || 'An unexpected error occurred'}
      </p>
      <Button onClick={resetErrorBoundary}>Try Again</Button>
    </div>
  );
}

// Export wrapper for consistent usage
export function AppErrorBoundary({ children }: { children: React.ReactNode }) {
  return (
    <ReactErrorBoundary
      FallbackComponent={ErrorFallback}
      onError={(error, errorInfo) => {
        console.error('Error boundary caught:', error, errorInfo);
        // Send to error tracking in production
      }}
      onReset={() => {
        // Reset app state if needed
        window.location.href = '/';
      }}
    >
      {children}
    </ReactErrorBoundary>
  );
}
```

#### Layered Error Boundaries (Industry Best Practice)

```typescript
// In App.tsx - implement layered boundaries
function App() {
  return (
    <AppErrorBoundary> {/* Global - catches everything */}
      <QueryProvider>
        <BrowserRouter>
          <Routes>
            <Route element={<ProtectedLayout />}>
              <Route 
                path="/leads" 
                element={
                  <RouteErrorBoundary> {/* Route-level */}
                    <Leads />
                  </RouteErrorBoundary>
                } 
              />
            </Route>
          </Routes>
        </BrowserRouter>
      </QueryProvider>
    </AppErrorBoundary>
  );
}
```

#### Handling Async Errors (Not Caught by Boundaries)

```typescript
// src/app/lib/errorHandler.ts
import { toast } from 'sonner';

export function handleApiError(error: unknown, context?: string): string {
  const message = error instanceof Error 
    ? error.message 
    : 'An unexpected error occurred';

  // Log for debugging
  console.error(`[${context || 'API Error'}]:`, error);

  // Show user-friendly message
  toast.error(message);

  // In production, report to error tracking
  if (import.meta.env.PROD) {
    // errorTrackingService.capture(error, { tags: { context } });
  }

  return message;
}

// Usage in async code
async function fetchData() {
  try {
    const data = await api.getData();
    return data;
  } catch (error) {
    handleApiError(error, 'Fetching data');
    throw error; // Re-throw if needed for TanStack Query
  }
}
```
```

---

## 5. Code Organization & Architecture

### 4.1 Recommended Folder Structure

```
src/
├── app/
│   ├── api/                    # API layer
│   │   ├── client.ts           # Unified HTTP client
│   │   ├── endpoints/          # Domain-specific endpoints
│   │   │   ├── leads.ts
│   │   │   ├── contacts.ts
│   │   │   └── deals.ts
│   │   ├── types.ts            # API types
│   │   └── index.ts            # Barrel export
│   │
│   ├── components/             # Shared components
│   │   ├── ui/                 # shadcn/ui primitives
│   │   ├── layout/             # Layout components
│   │   │   ├── AppHeader.tsx
│   │   │   ├── Sidebar.tsx
│   │   │   └── PageContainer.tsx
│   │   ├── forms/              # Reusable form components
│   │   │   ├── SearchInput.tsx
│   │   │   ├── DatePicker.tsx
│   │   │   └── SelectField.tsx
│   │   └── feedback/           # User feedback
│   │       ├── LoadingSpinner.tsx
│   │       ├── EmptyState.tsx
│   │       └── ErrorBoundary.tsx
│   │
│   ├── features/               # Feature-based modules
│   │   ├── leads/
│   │   │   ├── components/
│   │   │   ├── hooks/
│   │   │   ├── types.ts
│   │   │   └── index.ts
│   │   ├── deals/
│   │   ├── contacts/
│   │   └── pipeline/
│   │
│   ├── hooks/                  # Shared custom hooks
│   │   ├── useDebounce.ts
│   │   ├── useLocalStorage.ts
│   │   └── useMediaQuery.ts
│   │
│   ├── contexts/               # React contexts
│   │   ├── OrgContext.tsx
│   │   └── ThemeContext.tsx
│   │
│   ├── lib/                    # Utilities
│   │   ├── auth.ts
│   │   ├── utils.ts
│   │   └── constants.ts
│   │
│   ├── pages/                  # Route pages (thin wrappers)
│   │   ├── LeadsPage.tsx
│   │   ├── DealsPage.tsx
│   │   └── ...
│   │
│   └── App.tsx
│
├── styles/
│   ├── globals.css
│   └── theme.css
│
└── main.tsx
```

### 4.2 Feature-Based Module Pattern

Each feature should be self-contained:

```typescript
// src/app/features/leads/index.ts
export { LeadsList } from './components/LeadsList';
export { LeadForm } from './components/LeadForm';
export { LeadFilters } from './components/LeadFilters';
export { useLeads, useCreateLead, useUpdateLead } from './hooks/useLeads';
export type { LeadFormData, LeadFilterState } from './types';
```

### 4.3 Constants & Configuration

```typescript
// src/app/lib/constants.ts
export const ROUTES = {
  HOME: '/',
  DASHBOARD: '/dashboard',
  LEADS: '/leads',
  CONTACTS: '/contacts',
  DEALS: '/deals',
  PIPELINE: '/pipeline',
  SETTINGS: '/settings',
} as const;

export const PAGINATION = {
  DEFAULT_PAGE_SIZE: 20,
  PAGE_SIZE_OPTIONS: [10, 20, 50, 100],
} as const;

export const CACHE_TIMES = {
  SHORT: 1 * 60 * 1000,    // 1 minute
  MEDIUM: 5 * 60 * 1000,   // 5 minutes
  LONG: 30 * 60 * 1000,    // 30 minutes
} as const;
```

---

## 6. TypeScript Improvements

### 6.1 Enable Stricter Compiler Options ✅ IMPLEMENTED

> **Status**: ✅ Completed on February 5, 2026

**✅ What Was Implemented** in `tsconfig.json`:

```json
{
  "compilerOptions": {
    "strict": true,
    "noUnusedLocals": true,              // ✅ Now enabled (was false)
    "noUnusedParameters": true,          // ✅ Now enabled (was false)
    "noImplicitReturns": true,           // ✅ Added
    "noUncheckedIndexedAccess": true,    // ✅ Added
    "forceConsistentCasingInFileNames": true // ✅ Added
  }
}
```

**Note**: `exactOptionalPropertyTypes` was kept as `false` to avoid breaking existing code.

### 5.2 Use Discriminated Unions for State

```typescript
// Instead of multiple booleans:
type LoadingState<T> = 
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'success'; data: T }
  | { status: 'error'; error: Error };

// Usage:
const [state, setState] = useState<LoadingState<Lead[]>>({ status: 'idle' });

// Type-safe handling:
if (state.status === 'success') {
  // TypeScript knows state.data exists here
  return state.data.map(lead => ...);
}
```

### 5.3 Create Strict API Types

```typescript
// src/app/api/types.ts

// Use branded types for IDs to prevent mixing
type LeadId = string & { __brand: 'LeadId' };
type ContactId = string & { __brand: 'ContactId' };
type CompanyId = string & { __brand: 'CompanyId' };

// Helper to create branded types
export function createLeadId(id: string): LeadId {
  return id as LeadId;
}

// Use strict types for status fields
export type LeadStatus = 
  | 'New'
  | 'Open'
  | 'Contacted'
  | 'Qualified'
  | 'Unqualified'
  | 'Lost';

export interface Lead {
  id: LeadId;
  name: string;
  email: string;
  phone?: string;
  companyId?: CompanyId;
  status: LeadStatus;
  // ...
}
```

### 5.4 Use Zod for Runtime Validation

```bash
npm install zod
```

```typescript
// src/app/api/schemas.ts
import { z } from 'zod';

export const LeadSchema = z.object({
  id: z.string(),
  name: z.string().min(1),
  email: z.string().email(),
  phone: z.string().optional(),
  status: z.enum(['New', 'Open', 'Contacted', 'Qualified', 'Unqualified', 'Lost']),
});

export type Lead = z.infer<typeof LeadSchema>;

// Validate API responses
export async function getLeads(): Promise<Lead[]> {
  const response = await api.get<unknown[]>('/api/leads');
  return z.array(LeadSchema).parse(response);
}
```

---

## 7. API Layer Improvements

### 6.1 Add Request Interceptors

```typescript
// src/app/api/interceptors.ts
type RequestInterceptor = (config: RequestInit) => RequestInit;
type ResponseInterceptor = (response: Response) => Response | Promise<Response>;

const requestInterceptors: RequestInterceptor[] = [];
const responseInterceptors: ResponseInterceptor[] = [];

export function addRequestInterceptor(fn: RequestInterceptor) {
  requestInterceptors.push(fn);
}

export function addResponseInterceptor(fn: ResponseInterceptor) {
  responseInterceptors.push(fn);
}

// Logging interceptor
addRequestInterceptor((config) => {
  if (import.meta.env.DEV) {
    console.log('[API Request]', config);
  }
  return config;
});

// Error tracking interceptor
addResponseInterceptor(async (response) => {
  if (!response.ok && response.status >= 500) {
    // Log to error tracking service
    console.error('[API Error]', response.status, await response.clone().text());
  }
  return response;
});
```

### 6.2 Add Retry Logic

```typescript
// src/app/api/retry.ts
interface RetryConfig {
  maxRetries: number;
  retryDelay: number;
  retryOn: number[];
}

const defaultConfig: RetryConfig = {
  maxRetries: 3,
  retryDelay: 1000,
  retryOn: [408, 429, 500, 502, 503, 504],
};

export async function fetchWithRetry(
  url: string,
  init?: RequestInit,
  config = defaultConfig
): Promise<Response> {
  let lastError: Error | null = null;
  
  for (let attempt = 0; attempt <= config.maxRetries; attempt++) {
    try {
      const response = await fetch(url, init);
      
      if (config.retryOn.includes(response.status) && attempt < config.maxRetries) {
        await sleep(config.retryDelay * Math.pow(2, attempt)); // Exponential backoff
        continue;
      }
      
      return response;
    } catch (error) {
      lastError = error as Error;
      if (attempt < config.maxRetries) {
        await sleep(config.retryDelay * Math.pow(2, attempt));
      }
    }
  }
  
  throw lastError ?? new Error('Request failed after retries');
}

function sleep(ms: number) {
  return new Promise(resolve => setTimeout(resolve, ms));
}
```

### 6.3 Remove Mock Data Layer

**Problem**: The dual mock/real API pattern adds complexity.

**Current**:
```typescript
export async function getLeads(): Promise<Lead[]> {
  if (isUsingRealApi()) {
    // Real API call
  }
  await delay(300);
  return [...mockLeads]; // Mock fallback
}
```

**Recommendation**: Remove mock layer for production, use MSW for testing:

```bash
npm install msw --save-dev
```

```typescript
// src/mocks/handlers.ts (for testing only)
import { rest } from 'msw';
import { mockLeads } from './data';

export const handlers = [
  rest.get('/api/leads', (req, res, ctx) => {
    return res(ctx.json(mockLeads));
  }),
];
```

---

## 8. CSS & Styling Improvements

### 8.1 Current State Analysis

Your `theme.css` is well-organized with:
- CSS custom properties for theming (colors, spacing, typography)
- Dark mode support
- Responsive breakpoints
- Glass morphism effects
- Gradient backgrounds

**Strengths**:
- Good use of CSS custom properties
- Consistent spacing scale
- Proper dark mode implementation
- Brand color system with 60/30/10 rule

### 8.2 Issues Found

**1. Duplicate Color Systems**

You have colors defined in multiple places:
- `theme.css` - CSS custom properties
- Tailwind config (implicit via shadcn/ui)
- Inline Tailwind classes throughout components

**Example of inconsistency** (from `Pipeline.tsx`):
```typescript
// Hardcoded colors instead of using theme variables
const STAGE_COLORS = {
  Qualification: { bar: '#3b82f6', accent: 'text-blue-600', ... },
  // Should reference CSS custom properties
};
```

**2. Large Inline Styles in Components**

**From `Leads.tsx` (lines 182-193)**:
```typescript
const statusColors: Record<string, { bg: string; text: string; border: string; ... }> = {
  New: { bg: 'bg-blue-50', text: 'text-blue-700', border: 'border-blue-200', ... },
  Open: { bg: 'bg-cyan-50', text: 'text-cyan-700', ... },
  // 10 more entries...
};
```

**Solution**: Create utility classes in CSS:
```css
/* theme.css */
.status-new { @apply bg-blue-50 text-blue-700 border-blue-200; }
.status-open { @apply bg-cyan-50 text-cyan-700 border-cyan-200; }
/* ... */
```

### 8.3 CSS Best Practices to Implement

**1. Create a Design Tokens File**

```typescript
// src/app/lib/designTokens.ts
export const colors = {
  status: {
    new: { bg: 'bg-blue-50', text: 'text-blue-700', border: 'border-blue-200' },
    open: { bg: 'bg-cyan-50', text: 'text-cyan-700', border: 'border-cyan-200' },
    // ...
  },
  priority: {
    high: { bg: 'bg-red-50', text: 'text-red-700' },
    medium: { bg: 'bg-amber-50', text: 'text-amber-700' },
    // ...
  },
} as const;
```

**2. Use CSS Custom Properties for Dynamic Theming**

```css
/* Instead of scattered Tailwind classes */
.deal-card {
  --deal-color: var(--brand-primary);
  border-left: 4px solid var(--deal-color);
}

.deal-card[data-stage="qualification"] { --deal-color: #3b82f6; }
.deal-card[data-stage="proposal"] { --deal-color: #f59e0b; }
```

**3. Extract Common Patterns**

```css
/* theme.css - add component patterns */
.card-metric {
  @apply rounded-xl border bg-card p-6;
  @apply flex flex-col gap-2;
}

.stat-value {
  @apply text-3xl font-bold tracking-tight;
}

.stat-label {
  @apply text-sm text-muted-foreground;
}
```

### 8.4 Performance: CSS Loading

**Current**: All styles loaded in a single bundle.

**Recommendation**: Consider CSS code splitting for large pages:
```typescript
// For pages with unique styles
const Pipeline = lazy(() => 
  Promise.all([
    import('./pages/Pipeline'),
    import('./styles/pipeline.css'),
  ]).then(([module]) => module)
);
```

---

## 9. Implementation Roadmap

### Phase 1: Quick Wins (1-2 days)

**Priority**: Critical - Foundation for all other improvements

| Task | Files to Create/Modify | Impact |
|------|------------------------|--------|
| Add ESLint configuration | `.eslintrc.cjs` | Catch bugs early |
| Add Prettier configuration | `.prettierrc` | Consistent formatting |
| Add EditorConfig | `.editorconfig` | Cross-IDE consistency |
| Update tsconfig.json | `tsconfig.json` | Enable strict mode |
| Add lint scripts | `package.json` | Automate checks |
| Run auto-fix | N/A | Clean up codebase |

**Specific Changes to `tsconfig.json`**:
```json
{
  "compilerOptions": {
    "noUnusedLocals": true,        // Currently: false
    "noUnusedParameters": true,    // Currently: false
    "noImplicitReturns": true,     // Add
    "noUncheckedIndexedAccess": true  // Add
  }
}
```

### Phase 2: Data Layer (3-5 days)

**Priority**: High - Major DX and performance improvement

| Task | Files to Create/Modify | Impact |
|------|------------------------|--------|
| Install TanStack Query | `package.json` | Add dependency |
| Create QueryClient provider | `src/app/providers/QueryProvider.tsx` | Setup |
| Create useLeads hook | `src/app/hooks/api/useLeads.ts` | Replace manual fetching |
| Create useDeals hook | `src/app/hooks/api/useDeals.ts` | Replace manual fetching |
| Create useTasks hook | `src/app/hooks/api/useTasks.ts` | Replace manual fetching |
| Create useContacts hook | `src/app/hooks/api/useContacts.ts` | Replace manual fetching |
| Create useCompanies hook | `src/app/hooks/api/useCompanies.ts` | Replace manual fetching |
| Consolidate HTTP client | `src/app/api/client.ts` | Remove duplication |
| Delete http.ts | `src/app/api/http.ts` | Remove duplication |
| Add error handler utility | `src/app/lib/errorHandler.ts` | Consistent errors |
| Add error boundaries | `src/app/components/ErrorBoundary.tsx` | Graceful failures |

**Estimated Code Reduction**: ~500 lines of boilerplate

### Phase 3: Performance (3-5 days)

**Priority**: High - User-facing improvement

| Task | Files to Create/Modify | Impact |
|------|------------------------|--------|
| Add React.lazy to App.tsx | `src/app/App.tsx` | 60%+ smaller initial bundle |
| Create LazyPage wrapper | `src/app/components/LazyPage.tsx` | Suspense handling |
| Add bundle analyzer | `vite.config.ts` | Visibility into bundle |
| Optimize Lucide imports | All pages | Smaller bundle |
| Add react-virtual | `package.json` | Handle large lists |
| Implement virtual lists | `Leads.tsx`, `Pipeline.tsx` | Smooth scrolling |

**Lazy Loading Changes to `App.tsx`**:
```typescript
// BEFORE: Eager imports
import Leads from '@/app/pages/Leads';

// AFTER: Lazy imports
const Leads = lazy(() => import('@/app/pages/Leads'));
```

**Pages to Prioritize for Lazy Loading** (by size):
1. `Leads.tsx` (3,676 lines) - Critical
2. `Homepage.tsx` (2,498 lines) - Critical
3. `Pipeline.tsx` (2,252 lines) - Critical
4. `Settings.tsx` (2,147 lines) - Critical
5. `Tasks.tsx` (1,837 lines) - High

### Phase 4: Refactoring (1-2 weeks)

**Priority**: Medium - Long-term maintainability

#### Phase 4a: Extract Hooks (2-3 days)

| Hook to Extract | Source File | Target File |
|-----------------|-------------|-------------|
| useInView | `Homepage.tsx` | `src/app/hooks/useInView.ts` |
| useMousePosition | `Homepage.tsx` | `src/app/hooks/useMousePosition.ts` |
| useParallax | `Homepage.tsx` | `src/app/hooks/useParallax.ts` |
| useResponsiveNavCount | `AppHeader.tsx` | `src/app/hooks/useResponsiveNavCount.ts` |

#### Phase 4b: Extract Configurations (1-2 days)

| Config to Extract | Source File | Target File |
|-------------------|-------------|-------------|
| WIZARD_STEPS | `Leads.tsx` | `src/app/config/leads.config.ts` |
| statusConfig, priorityConfig | `Tasks.tsx` | `src/app/config/tasks.config.ts` |
| STAGE_COLORS, STAGE_ICONS | `Pipeline.tsx` | `src/app/config/pipeline.config.ts` |
| TAB_CONFIG | `Settings.tsx` | `src/app/config/settings.config.ts` |

#### Phase 4c: Split Large Pages (1 week)

**Leads.tsx Refactor Plan**:
```
src/app/features/leads/
├── index.ts                    # Public exports
├── LeadsPage.tsx               # Main page (< 200 lines)
├── components/
│   ├── LeadsList.tsx           # Table component
│   ├── LeadCard.tsx            # Individual card
│   ├── LeadFilters.tsx         # Filter controls
│   ├── AddLeadDialog/
│   │   ├── index.tsx           # Dialog wrapper
│   │   ├── ContactStep.tsx     # Wizard step 1
│   │   ├── CompanyStep.tsx     # Wizard step 2
│   │   ├── QualificationStep.tsx # Wizard step 3
│   │   └── NotesStep.tsx       # Wizard step 4
│   └── ConvertLeadDialog.tsx   # Conversion modal
├── hooks/
│   ├── useLeads.ts             # Data fetching
│   ├── useLeadFilters.ts       # Filter state
│   └── useLeadMutations.ts     # Create/update/delete
└── types.ts                    # Page-specific types
```

### Phase 5: Testing (Ongoing)

**Priority**: Medium - Quality assurance

| Task | Files to Create | Impact |
|------|-----------------|--------|
| Install Vitest | `package.json`, `vitest.config.ts` | Unit testing |
| Install React Testing Library | `package.json` | Component testing |
| Install MSW | `package.json` | API mocking |
| Create test setup | `src/test/setup.ts` | Test configuration |
| Create test utilities | `src/test/utils.tsx` | Render helpers |
| Write hook tests | `src/app/hooks/__tests__/*.test.ts` | Test hooks |
| Write component tests | `src/app/components/__tests__/*.test.tsx` | Test components |

**Test Coverage Targets**:
- Hooks: 90%+
- Utility functions: 95%+
- Components: 70%+
- Pages: 50%+ (integration tests)

### Implementation Order Summary

```mermaid
gantt
    title Frontend Improvement Timeline
    dateFormat  YYYY-MM-DD
    section Phase 1
    ESLint/Prettier Setup    :a1, 2026-02-06, 1d
    TypeScript Strict Mode   :a2, after a1, 1d
    section Phase 2
    TanStack Query Setup     :b1, after a2, 2d
    Custom Hooks             :b2, after b1, 3d
    section Phase 3
    Lazy Loading             :c1, after b2, 2d
    Bundle Optimization      :c2, after c1, 3d
    section Phase 4
    Extract Hooks            :d1, after c2, 2d
    Extract Configs          :d2, after d1, 2d
    Split Large Pages        :d3, after d2, 5d
    section Phase 5
    Testing Setup            :e1, after d3, 2d
    Write Tests              :e2, after e1, 5d
```

---

## Appendix: Recommended Dependencies

### Add (Production)

```json
{
  "@tanstack/react-query": "^5.x",
  "@tanstack/react-virtual": "^3.x",
  "@tanstack/react-query-devtools": "^5.x",
  "zod": "^3.x"
}
```

### Add (Development)

```json
{
  "@typescript-eslint/eslint-plugin": "^7.x",
  "@typescript-eslint/parser": "^7.x",
  "eslint": "^8.x",
  "eslint-config-prettier": "^9.x",
  "eslint-plugin-react": "^7.x",
  "eslint-plugin-react-hooks": "^4.x",
  "eslint-plugin-react-refresh": "^0.4.x",
  "prettier": "^3.x",
  "vite-plugin-visualizer": "^0.10.x",
  "vitest": "^1.x",
  "@testing-library/react": "^14.x",
  "msw": "^2.x"
}
```

### Consider Removing

| Package | Reason | Replacement |
|---------|--------|-------------|
| `@mui/material` | Duplicate of Radix/shadcn | Use shadcn/ui only |
| `@emotion/react` | Only needed for MUI | Remove with MUI |
| `@emotion/styled` | Only needed for MUI | Remove with MUI |
| `react-dnd` | Complex, could use native | Consider @dnd-kit |

---

## Appendix C: Testing Setup (2025 Industry Standard)

> Vitest + React Testing Library is the current industry standard, replacing Jest for Vite-based projects.

### Vitest Configuration

```typescript
// vitest.config.ts
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: 'happy-dom', // Faster than jsdom
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.{test,spec}.{ts,tsx}'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'html'],
      exclude: ['node_modules/', 'src/test/', '**/*.d.ts'],
    },
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
});
```

### Test Setup File

```typescript
// src/test/setup.ts
import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, beforeAll, afterAll } from 'vitest';
import { server } from './mocks/server';

// Start MSW server before all tests
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));

// Reset handlers after each test
afterEach(() => {
  cleanup();
  server.resetHandlers();
});

// Close server after all tests
afterAll(() => server.close());
```

### MSW Mock Handlers (Industry Standard for API Mocking)

```typescript
// src/test/mocks/handlers.ts
import { http, HttpResponse } from 'msw';
import { mockLeads, mockDeals, mockContacts } from './data';

export const handlers = [
  http.get('/api/leads', () => {
    return HttpResponse.json(mockLeads);
  }),
  
  http.post('/api/leads', async ({ request }) => {
    const body = await request.json();
    return HttpResponse.json({ id: 'new-id', ...body }, { status: 201 });
  }),
  
  http.get('/api/deals', () => {
    return HttpResponse.json(mockDeals);
  }),
];

// src/test/mocks/server.ts
import { setupServer } from 'msw/node';
import { handlers } from './handlers';

export const server = setupServer(...handlers);
```

### Test Utilities with Providers

```typescript
// src/test/utils.tsx
import { ReactNode } from 'react';
import { render, RenderOptions } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter } from 'react-router-dom';

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
    },
  });
}

function AllProviders({ children }: { children: ReactNode }) {
  const queryClient = createTestQueryClient();
  
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>{children}</BrowserRouter>
    </QueryClientProvider>
  );
}

export function renderWithProviders(
  ui: React.ReactElement,
  options?: Omit<RenderOptions, 'wrapper'>
) {
  return render(ui, { wrapper: AllProviders, ...options });
}

export * from '@testing-library/react';
export { renderWithProviders as render };
```

### Example Component Test

```typescript
// src/app/features/leads/__tests__/LeadsList.test.tsx
import { describe, it, expect } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { render } from '@/test/utils';
import { LeadsList } from '../components/LeadsList';

describe('LeadsList', () => {
  it('renders loading state initially', () => {
    render(<LeadsList />);
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
  });

  it('renders leads after loading', async () => {
    render(<LeadsList />);
    
    await waitFor(() => {
      expect(screen.getByText('John Doe')).toBeInTheDocument();
    });
  });

  it('opens create dialog when clicking add button', async () => {
    const user = userEvent.setup();
    render(<LeadsList />);
    
    await user.click(screen.getByRole('button', { name: /add lead/i }));
    
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });
});
```

### NPM Scripts for Testing

```json
{
  "scripts": {
    "test": "vitest",
    "test:ui": "vitest --ui",
    "test:coverage": "vitest run --coverage",
    "test:watch": "vitest --watch"
  }
}

---

## 10. File-by-File Analysis

### Page Components Analysis

#### `Leads.tsx` (3,676 lines) - CRITICAL

**Issues Found**:
1. **AddLeadDialog component** defined inside file (lines 98-600+) - 500+ lines
2. **WIZARD_STEPS config** inline (lines 66-95) - 30 lines
3. **sourceIcons & statusColors** mappings inline (lines 168-193) - 25 lines
4. **10+ useState hooks** at component root
5. **Complex form state management** without react-hook-form
6. **LeadForm type** defined inline

**Specific Code Smells**:
```typescript
// Line 321 - Inline state update pattern repeated 20+ times
onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}

// Lines 168-193 - Large inline config object
const sourceIcons: Record<string, string> = { website: '🌐', ... };
const statusColors: Record<string, { bg: string; ... }> = { ... };
```

**Refactoring Plan**:
```
features/leads/
├── components/
│   ├── AddLeadDialog/          # 500+ lines extracted
│   ├── LeadCard.tsx
│   ├── LeadFilters.tsx
│   └── LeadsList.tsx
├── config/
│   ├── wizardSteps.ts
│   ├── statusColors.ts
│   └── sourceIcons.ts
├── hooks/
│   └── useLeadForm.ts          # Form state management
└── types.ts
```

---

#### `Companies.tsx` (1,120 lines) - HIGH

**Issues Found**:
1. **INDUSTRIES & COMPANY_SIZES** arrays inline (lines 49-71)
2. **useEffect with Promise.all** for data fetching (lines 93-107)
3. **5 useMemo hooks** that could be extracted
4. **Repeated filter/sort logic** similar to Contacts.tsx

**Pattern Found** (repeated in 6 files):
```typescript
// Lines 93-107 - This exact pattern appears in Companies, Contacts, Activities, Team
useEffect(() => {
  let cancelled = false;
  setLoading(true);
  Promise.all([getCompanies(), getContacts(), getDeals()])
    .then(([companiesData, contactsData, dealsData]) => { 
      if (!cancelled) {
        setCompanies(companiesData);
        setContacts(contactsData);
        setDeals(dealsData);
      }
    })
    .catch(() => {})  // Silent fail - should log
    .finally(() => { if (!cancelled) setLoading(false); });
  return () => { cancelled = true; };
}, []);
```

---

#### `Contacts.tsx` (1,089 lines) - HIGH

**Issues Found**:
1. **Same data fetching pattern** as Companies.tsx
2. **contactStats useMemo** with complex date calculations (lines 86-116)
3. **filteredContacts useMemo** with sorting logic (lines 118-183)
4. **No reusable filter components**

**Shared Logic Opportunity**: `Contacts.tsx` and `Companies.tsx` share 80% identical filter/sort logic.

---

#### `Activities.tsx` (981 lines) - MEDIUM

**Issues Found**:
1. **ACTIVITY_TYPES config** inline (lines 50-58)
2. **3 date formatting functions** inline (lines 60-93)
3. **2 separate useEffects** for related data (lines 139-164)
4. **Complex conditional data fetching** based on filter state

**Extractable Utilities**:
```typescript
// Lines 60-93 - Should be in utils/dateFormatters.ts
function formatDate(iso: string): string { ... }
function formatRelativeDate(iso: string): string { ... }
function getDateGroup(iso: string): string { ... }
```

---

#### `Team.tsx` (1,127 lines) - HIGH

**Issues Found**:
1. **ROLES config** inline (lines 64-69)
2. **StatCard component** defined inline (lines 76-103)
3. **MemberDetailPanel component** defined inline (lines 106-200+)
4. **Mock stats generation** in component (lines 123-130)

**Specific Problem** (lines 123-130):
```typescript
// Mock data in production code - should be removed or conditional
const mockStats = {
  leadsAssigned: Math.floor(Math.random() * 50) + 5,
  dealsWon: Math.floor(Math.random() * 20),
  // ...
};
```

---

#### `Templates.tsx` (512 lines) - LOW

**Status**: Well-structured, but has minor issues

**Issues Found**:
1. **categoryIcons & COPY_TYPES** inline (lines 19-33)
2. **emptyFormData** object inline (lines 58-68)
3. Uses the same data fetching anti-pattern

**Good Patterns to Keep**:
- Uses TypeScript interfaces for form data
- Clear function naming
- Reasonable file size

---

### API Layer Analysis

#### `apiClient.ts` (62 lines) - KEEP

**Status**: Well-structured

**Good Patterns**:
- Single responsibility
- Type-safe with generics
- Proper error handling
- Organization ID header injection

---

#### `http.ts` (77 lines) - SHOULD REMOVE

**Status**: Duplicate of apiClient.ts

**Issue**: Provides axios-style wrapper but overlaps with apiClient.ts functionality. This creates confusion.

**Recommendation**: Delete and migrate usages to apiClient.ts

---

#### Domain API Files (`leads.ts`, `deals.ts`, `contacts.ts`, etc.)

**Consistent Pattern Found**:
```typescript
// All follow this structure:
type EntityRaw = { ... };          // Raw API response type
function mapEntity(d: EntityRaw): Entity { ... }  // Mapper function

export async function getEntities(): Promise<Entity[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<EntityRaw[]>('/api/entities');
    return Array.isArray(list) ? list.map(mapEntity) : [];
  }
  await delay(300);
  return [...mockEntities];  // Mock fallback
}
```

**Issues**:
1. Mock data fallback adds complexity
2. Mapping functions are duplicated
3. No error boundaries
4. No request caching

---

### Component Analysis

#### `RequireAuth.tsx` (17 lines) - EXCELLENT

**Status**: Perfect size, single responsibility

```typescript
// Clean, simple, does one thing well
export default function RequireAuth({ children }: { children: React.ReactNode }) {
  const location = useLocation();
  const user = getCurrentUser();
  if (user == null) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }
  return <>{children}</>;
}
```

---

#### `LoadingSpinner.tsx` (33 lines) - EXCELLENT

**Status**: Well-structured, proper accessibility

**Good Patterns**:
- Size variants
- ARIA labels
- sr-only text for screen readers
- Composable (with/without label)

---

#### `AppHeader.tsx` (374 lines) - MEDIUM

**Issues Found**:
1. **allNavItems config** inline (lines 43-58)
2. **useResponsiveNavCount hook** defined inline (lines 114-155)
3. **NavLink & NavLinks components** defined inline (lines 71-111)

**Extractable**:
- `useResponsiveNavCount` → `hooks/useResponsiveNavCount.ts`
- `allNavItems` → `config/navigation.ts`

---

### Context Analysis

#### `OrgContext.tsx` (132 lines) - GOOD

**Status**: Well-structured context implementation

**Good Patterns**:
- Proper TypeScript types
- useMemo for value object
- useCallback for actions
- Handles both demo and real API modes

**Minor Issues**:
- Could extract `DEMO_ORGANIZATIONS` to config file
- `hasFetched` state logic is complex

---

### Utility Analysis

#### `auth.ts` (84 lines) - GOOD

**Status**: Clean, focused utility

**Good Patterns**:
- Clear function names
- Try-catch for localStorage errors
- Type exports

**Minor Issue**: Functions like `getDemoUser` are aliases - could simplify

---

### Summary Table

| File | Lines | Status | Action |
|------|-------|--------|--------|
| `Leads.tsx` | 3,676 | Critical | Split into feature module |
| `Homepage.tsx` | 2,498 | Critical | Extract hooks & components |
| `Pipeline.tsx` | 2,252 | Critical | Extract configs & utils |
| `Settings.tsx` | 2,147 | High | Split by tab |
| `Tasks.tsx` | 1,837 | High | Extract Kanban components |
| `Companies.tsx` | 1,120 | Medium | Extract shared filter logic |
| `Contacts.tsx` | 1,089 | Medium | Extract shared filter logic |
| `Team.tsx` | 1,127 | Medium | Remove mock data, extract panels |
| `Activities.tsx` | 981 | Medium | Extract date utilities |
| `Templates.tsx` | 512 | Low | Minor cleanup |
| `http.ts` | 77 | Remove | Delete, use apiClient only |
| `RequireAuth.tsx` | 17 | Keep | Excellent |
| `LoadingSpinner.tsx` | 33 | Keep | Excellent |

### Files with Quick Wins

| File | Quick Win | Time |
|------|-----------|------|
| `App.tsx` | Add lazy loading for routes | 15 min |
| `apiClient.ts` + `http.ts` | Delete http.ts, update imports | 30 min |
| `tsconfig.json` | Enable strict options | 5 min |
| `Activities.tsx` | Extract date formatters to utils | 10 min |
| `Team.tsx` | Remove mock stats or make conditional | 5 min |

---

## 11. Metrics to Track

### Before/After Comparison

Track these metrics to measure improvement:

| Metric | How to Measure | Current (Est.) | Target |
|--------|----------------|----------------|--------|
| Initial Bundle Size | `npm run build` | ~2MB | < 500KB |
| Largest Chunk | Vite build output | ~800KB | < 200KB |
| Time to Interactive | Lighthouse | ~3s | < 1.5s |
| Lines per Component | `wc -l` | 3,676 max | < 400 |
| useEffect for fetching | `grep -c` | 37 | 0 |
| Manual loading states | `grep -c` | 11 | 0 |
| ESLint Errors | `npm run lint` | N/A | 0 |
| Test Coverage | Vitest | 0% | 70%+ |

---

## Summary

### Top 10 Most Impactful Changes

1. **Add ESLint + Prettier** - Immediate quality improvement, catches bugs
2. **Add TanStack Query** - Eliminates 500+ lines of boilerplate
3. **Lazy load routes in App.tsx** - 60%+ smaller initial bundle
4. **Split Leads.tsx into feature module** - Most complex page, best ROI
5. **Consolidate HTTP clients** - Remove confusion, single source of truth
6. **Extract hooks from Homepage.tsx** - Reusable across codebase
7. **Enable strict TypeScript** - Catch bugs at compile time
8. **Add Error Boundaries** - Graceful failure handling
9. **Extract configuration objects** - Cleaner, more testable code
10. **Add virtual scrolling** - Handle large datasets smoothly

### Estimated Impact

| Area | Current Pain | After Implementation |
|------|--------------|----------------------|
| Developer Experience | Manual loading states everywhere | Auto-caching, optimistic updates |
| Performance | 2MB+ initial load | < 500KB initial load |
| Maintainability | 3,600 line files | < 400 line files |
| Code Quality | No linting | Zero lint errors |
| Testing | No tests | 70%+ coverage |
| Error Handling | Inconsistent | Centralized, user-friendly |

### Getting Started

Run these commands to begin Phase 1:

```bash
# Install dev dependencies
npm install -D eslint @typescript-eslint/eslint-plugin @typescript-eslint/parser \
  eslint-config-prettier eslint-plugin-react eslint-plugin-react-hooks \
  eslint-plugin-react-refresh prettier

# Create configuration files (copy from this report)
# Then run:
npm run lint -- --fix

# Update tsconfig.json strict options, then:
npx tsc --noEmit
```

Following this guide will significantly improve code quality, maintainability, and performance while establishing patterns that scale as the application grows.

---

## Appendix A: Sample Configuration Files

### `.eslintrc.cjs` (2025 Industry Standard Configuration)

```javascript
module.exports = {
  root: true,
  env: { browser: true, es2020: true },
  extends: [
    'eslint:recommended',
    'plugin:@typescript-eslint/recommended',
    'plugin:@typescript-eslint/recommended-type-checked', // Type-aware rules
    'plugin:react/recommended',
    'plugin:react/jsx-runtime',
    'plugin:react-hooks/recommended',
    'prettier' // MUST be last to override formatting rules
  ],
  ignorePatterns: ['dist', '.eslintrc.cjs', 'node_modules'],
  parser: '@typescript-eslint/parser',
  parserOptions: {
    ecmaVersion: 2023,
    sourceType: 'module',
    project: ['./tsconfig.json'],
    ecmaFeatures: { jsx: true }
  },
  plugins: ['react-refresh', '@typescript-eslint'],
  settings: { 
    react: { version: 'detect' }  // Auto-detect React version
  },
  rules: {
    // React Refresh (Vite HMR)
    'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
    
    // TypeScript - Enforce quality
    '@typescript-eslint/no-unused-vars': ['error', { 
      argsIgnorePattern: '^_',
      varsIgnorePattern: '^_'
    }],
    '@typescript-eslint/no-explicit-any': 'warn',
    '@typescript-eslint/prefer-nullish-coalescing': 'warn',
    '@typescript-eslint/prefer-optional-chain': 'warn',
    
    // React - Best practices
    'react/prop-types': 'off', // Using TypeScript instead
    'react/react-in-jsx-scope': 'off', // Not needed in React 17+
    'react-hooks/rules-of-hooks': 'error',
    'react-hooks/exhaustive-deps': 'warn',
    
    // General quality
    'no-console': ['warn', { allow: ['warn', 'error'] }],
    'prefer-const': 'error',
    'no-var': 'error',
  },
};
```

### `.prettierrc`

```json
{
  "semi": true,
  "singleQuote": true,
  "tabWidth": 2,
  "trailingComma": "es5",
  "printWidth": 100,
  "bracketSpacing": true,
  "arrowParens": "always"
}
```

### `.editorconfig`

```ini
root = true

[*]
indent_style = space
indent_size = 2
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.md]
trim_trailing_whitespace = false
```

---

## Appendix B: Recommended Dependencies

### Production Dependencies to Add

```json
{
  "@tanstack/react-query": "^5.x",
  "@tanstack/react-virtual": "^3.x",
  "zod": "^3.x"
}
```

### Development Dependencies to Add

```json
{
  "@typescript-eslint/eslint-plugin": "^7.x",
  "@typescript-eslint/parser": "^7.x",
  "eslint": "^8.x",
  "eslint-config-prettier": "^9.x",
  "eslint-plugin-react": "^7.x",
  "eslint-plugin-react-hooks": "^4.x",
  "eslint-plugin-react-refresh": "^0.4.x",
  "prettier": "^3.x",
  "vite-plugin-visualizer": "^0.10.x",
  "vitest": "^1.x",
  "@testing-library/react": "^14.x",
  "msw": "^2.x",
  "happy-dom": "^13.x"
}
```

### Dependencies to Consider Removing

| Package | Reason | Replacement |
|---------|--------|-------------|
| `@mui/material` | Duplicate of Radix/shadcn | Use shadcn/ui only |
| `@emotion/react` | Only needed for MUI | Remove with MUI |
| `@emotion/styled` | Only needed for MUI | Remove with MUI |
| `react-dnd` | Complex, could use native | Consider alternatives |

---

## Appendix C: Testing Setup (2025 Industry Standard) ✅ IMPLEMENTED

> **Status**: ✅ Testing infrastructure implemented on February 5, 2026
> - Vitest configured with happy-dom
> - MSW handlers for API mocking
> - Test utilities with QueryClient wrapper
> - 4 test files (27 passing tests)
>   - `useDebounce.test.ts` - Hook tests
>   - `LoadingSpinner.test.tsx` - Component tests
>   - `EmptyState.test.tsx` - Component tests (8 tests)
>   - `ErrorBoundary.test.tsx` - Component tests (8 tests)

### `vitest.config.ts` ✅ Created

```typescript
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import { resolve } from 'path';

export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: 'happy-dom',
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.{test,spec}.{ts,tsx}'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'html'],
      exclude: [
        'node_modules/',
        'src/test/',
        '**/*.d.ts',
        '**/*.config.*',
      ],
    },
  },
  resolve: {
    alias: {
      '@': resolve(__dirname, './src'),
    },
  },
});
```

### `src/test/setup.ts`

```typescript
import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, beforeAll, afterAll } from 'vitest';
import { server } from './mocks/server';

// Start MSW server before all tests
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));

// Reset handlers after each test
afterEach(() => {
  cleanup();
  server.resetHandlers();
});

// Close server after all tests
afterAll(() => server.close());
```

### `src/test/mocks/handlers.ts` (MSW 2.x)

```typescript
import { http, HttpResponse } from 'msw';

export const handlers = [
  http.get('/api/leads', () => {
    return HttpResponse.json([
      { id: '1', name: 'Test Lead', email: 'test@example.com' },
    ]);
  }),
  
  http.get('/api/companies', () => {
    return HttpResponse.json([
      { id: '1', name: 'Test Company' },
    ]);
  }),
  
  // Add more handlers as needed
];
```

### `src/test/mocks/server.ts`

```typescript
import { setupServer } from 'msw/node';
import { handlers } from './handlers';

export const server = setupServer(...handlers);
```

### Example Test File

```typescript
// src/features/leads/components/LeadCard.test.tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { LeadCard } from './LeadCard';

describe('LeadCard', () => {
  const mockLead = {
    id: '1',
    name: 'John Doe',
    email: 'john@example.com',
    status: 'new',
  };

  it('renders lead information', () => {
    render(<LeadCard lead={mockLead} />);
    
    expect(screen.getByText('John Doe')).toBeInTheDocument();
    expect(screen.getByText('john@example.com')).toBeInTheDocument();
  });

  it('calls onClick when clicked', async () => {
    const user = userEvent.setup();
    const handleClick = vi.fn();
    
    render(<LeadCard lead={mockLead} onClick={handleClick} />);
    
    await user.click(screen.getByRole('button'));
    
    expect(handleClick).toHaveBeenCalledWith(mockLead);
  });
});
```

---

## Appendix D: Refactoring Checklist

Use this checklist when refactoring each file:

### Before Refactoring
- [ ] Read the entire file and understand its purpose
- [ ] Identify all exports and their consumers
- [ ] List all state variables and their relationships
- [ ] Note all API calls and their dependencies
- [ ] Check for inline components/hooks/configs

### During Refactoring
- [ ] Extract inline configurations to `config/` files
- [ ] Extract inline components to separate files
- [ ] Extract custom hooks to `hooks/` directory
- [ ] Replace manual data fetching with TanStack Query
- [ ] Add proper TypeScript types
- [ ] Add error boundaries where appropriate

### After Refactoring
- [ ] Run `npm run lint` and fix issues
- [ ] Run `npx tsc --noEmit` to check types
- [ ] Test all functionality manually
- [ ] Write unit tests for extracted components
- [ ] Update imports in consuming files
- [ ] Document any breaking changes

### File Size Guidelines
| Size | Status | Action |
|------|--------|--------|
| < 200 lines | ✅ Good | Keep as is |
| 200-400 lines | ⚠️ Review | Consider splitting |
| 400-800 lines | ⛔ Large | Should split |
| > 800 lines | 🚨 Critical | Must split |

---

## Appendix E: Quick Commands Reference

```bash
# Install all recommended dependencies
npm install @tanstack/react-query @tanstack/react-virtual zod

# Install dev dependencies
npm install -D @typescript-eslint/eslint-plugin @typescript-eslint/parser \
  eslint eslint-config-prettier eslint-plugin-react eslint-plugin-react-hooks \
  eslint-plugin-react-refresh prettier vite-plugin-visualizer vitest \
  @testing-library/react @testing-library/jest-dom @testing-library/user-event \
  msw happy-dom

# Add scripts to package.json
npm pkg set scripts.lint="eslint src --ext ts,tsx --report-unused-disable-directives --max-warnings 0"
npm pkg set scripts.format="prettier --write src"
npm pkg set scripts.typecheck="tsc --noEmit"
npm pkg set scripts.test="vitest"
npm pkg set scripts.test:coverage="vitest run --coverage"

# Run quality checks
npm run lint -- --fix
npm run format
npm run typecheck

# Analyze bundle size
npm run build && npx vite-bundle-visualizer
```

---

*Report generated: February 2026*  
*For questions or clarifications, refer to the codebase analysis above.*

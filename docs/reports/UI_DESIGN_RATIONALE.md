# UI Design Rationale

Why the Cadence frontend looks and behaves the way it does: design system, layout patterns, accessibility, and component choices.

---

## 1. Design Goals

- **Clarity:** Sales and CRM workflows (leads, pipeline, tasks, copy) should be easy to scan and act on.
- **Trust:** Professional, consistent UI so users feel confident connecting their CRM and using Intelligent Sales Writer.
- **Accessibility:** Keyboard navigation, focus management, skip links, and semantic structure for screen readers.
- **Responsiveness:** Works on desktop and mobile with a single header pattern (nav bar + mobile sheet).

---

## 2. Why These Colors?

### Orange as primary

- **Orange** (`orange-500`–`orange-700`) is used for:
  - Logo (Sparkles in gradient `from-orange-600 to-orange-500`)
  - Primary buttons and CTAs
  - Active nav items (`bg-orange-100 text-orange-700`)
  - Focus rings (`ring-orange-500`)
  - Accent badges and links

**Rationale:** Orange is energetic and action-oriented without feeling as aggressive as red. It stands out against neutrals and reads as “primary action” and “brand” across the app. It’s used sparingly so it stays meaningful.

### Slate neutrals

- **Slate** (`slate-50`–`slate-900`) is used for:
  - Backgrounds (`bg-slate-50` on app pages)
  - Text (`text-slate-900` headings, `text-slate-600` body/secondary)
  - Borders (`border-slate-200`)

**Rationale:** Slate is a neutral that works in both light and (future) dark themes. It’s softer than pure gray and keeps the UI calm and readable. Page backgrounds use `slate-50` to separate content from the white header/cards.

### Theme variables

- **CSS variables** in `theme.css` (`--primary`, `--background`, `--foreground`, etc.) define the design tokens. Tailwind’s `@theme inline` maps these so components can use `bg-background`, `text-foreground`, etc.
- **Rationale:** One place to change colors and spacing; supports a future dark mode (`.dark` overrides already exist).

---

## 3. Why This Layout?

### Two header patterns

1. **Public (Homepage, Login, Privacy, Terms)**  
   - Minimal header: logo + “Sign In” (or “Back to Home”).  
   - **Why:** These pages are entry or legal; focus is on content and one clear CTA, not full app nav.

2. **App (Dashboard, Leads, Pipeline, etc.)**  
   - Full `AppHeader`: logo → main nav (Dashboard, Leads, Pipeline, Tasks, Activities, Contacts, Companies, Templates, History, Help) → user avatar dropdown (Settings, Sign out).  
   - **Why:** Logged-in users need fast switching between CRM and copy features; sticky header keeps nav always available.

### Page structure

- **Main content:** `<main id={MAIN_CONTENT_ID}>` with `tabIndex={-1}` so “Skip to main content” lands correctly.
- **Horizontal padding:** `px-[var(--page-padding)]` (1rem) so content doesn’t touch viewport edges and stays consistent.
- **Max width:** List/detail pages often use `max-w-4xl` or `max-w-6xl` so lines don’t get too long on large screens.

**Rationale:** Predictable structure improves accessibility and makes it easy to add new pages that feel part of the same app.

### Cards and containers

- **Cards:** `rounded-2xl`, white background, subtle borders or shadows. Used for forms, stats, and feature blocks.
- **Empty states:** Dashed border (`border-dashed border-slate-200`), centered icon + text + optional CTA.
- **Rationale:** Cards group related content; rounded corners and soft borders keep the UI friendly rather than boxy.

---

## 4. Why These Components?

### Shared primitives

- **SkipLink:** Visible on keyboard focus; jumps to `#main-content`. **Why:** Meets accessibility expectations and avoids forcing keyboard users through the whole header.
- **LoadingSpinner:** Single, consistent loading indicator (orange accent). **Why:** Same loading experience everywhere.
- **EmptyState:** Icon + title + optional description + optional CTA (link or button). **Why:** Clear “no data” state and a path to fix it (e.g. “Go to Dashboard”, “Add lead”).

### shadcn/ui

- Buttons, inputs, selects, dialogs, dropdowns, sheets, etc. come from **shadcn/ui** (Radix primitives + Tailwind).
- **Why:** Accessible by default (focus, roles, keyboard), consistent API, and styles are in the repo so we can tweak them (e.g. orange focus rings, slate borders).

### Lucide icons

- **Why:** Single icon set (Lucide) keeps style consistent; icons are used for nav, empty states, and actions so the UI is scannable.

---

## 5. Why These Patterns?

### Forms

- **Labels** above or beside inputs; **primary button** for submit (e.g. “Generate copy”, “Save & Continue”).
- **Validation:** Required fields and errors surfaced via toasts or inline messages.
- **Why:** Familiar form pattern; reduces cognitive load when generating copy or editing leads/deals/tasks.

### Toasts (Sonner)

- **Position:** `top-center`; **richColors** and **closeButton**.
- **Why:** Non-blocking feedback for success/error; center-top is visible without covering primary content.

### Navigation and state

- **React Router** with `Link` and `useNavigate`; **state** used for flow data (e.g. `copy`/`copyTypeLabel` to Send to CRM, `templateId` to Dashboard, `regenerateContext` for History → Dashboard).
- **Why:** Users can move between Dashboard → Generated → Send to CRM (or Templates → Dashboard) without losing context; no global store needed for these flows.

### Mobile

- **Header:** Nav hidden on small screens; **Sheet** (slide-over) with same nav links + Settings + Sign out.
- **Why:** Full nav would be cramped; sheet keeps all links available without a heavy drawer.

---

## 6. Why These Animations?

- **Homepage:** `animate-fade-in`, `animate-fade-in-up`, `animate-blob` (soft moving blobs) with staggered delays.
- **Rationale:** Gentle motion draws attention to hero and sections without being distracting; blobs add a bit of “product” feel.
- **App pages:** Mostly no animation beyond transitions in dialogs/sheets.
- **Rationale:** In-app focus is on speed and clarity; minimal motion keeps the CRM/copy workflow calm.

---

## 7. Typography and Spacing

- **Base font size:** `--font-size: 16px` in `theme.css`; scale (`--text-base`, `--text-lg`, etc.) for headings and body.
- **Weights:** Medium for labels/buttons, normal for body. **Why:** Clear hierarchy without too many weights.
- **Spacing:** `--page-padding`, Tailwind spacing scale, and consistent `gap`/`py`/`px` in grids and stacks.
- **Rationale:** Consistent type and spacing make the app feel coherent and easier to maintain.

---

## 8. Summary Table

| Choice | Reason |
|--------|--------|
| Orange primary | Energetic, action-oriented, distinct from neutrals |
| Slate neutrals | Calm, readable, theme-ready |
| Two header types | Public = minimal; App = full nav + user menu |
| SkipLink + main id | Keyboard and screen-reader accessibility |
| Cards + rounded-2xl | Grouping and friendly, modern look |
| shadcn/ui | Accessible, customizable components |
| Sonner top-center | Visible, non-blocking feedback |
| Route state for flows | Preserve context across Dashboard/Generated/Send/Templates |
| Mobile sheet nav | Full nav without crowding the header |
| Light animation on homepage only | Engagement on landing; calm in app |

---

**Related:** [FRONTEND_PAGES_REPORT.md](./FRONTEND_PAGES_REPORT.md) (routes and pages), [USER_FLOWS_REPORT.md](./USER_FLOWS_REPORT.md) (user journeys), [PROJECT_ASPECTS.md](../../PROJECT_ASPECTS.md) (every aspect).

*Last updated: February 2026*

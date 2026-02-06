# Frontend Coding Standards

## File Naming
- **Components**: PascalCase (e.g., `AppHeader.tsx`, `LoadingSpinner.tsx`)
- **Hooks**: camelCase with `use` prefix (e.g., `useDebounce.ts`, `useLocalStorage.ts`)
- **API modules**: camelCase (e.g., `contacts.ts`, `leadStatuses.ts`)
- **Config files**: camelCase (e.g., `leadConfig.ts`, `navigation.ts`)
- **Page folders**: camelCase (e.g., `leads/`, `dashboard/`, `settings/`)

## Page Module Structure
When a page grows beyond 500 lines, extract into a module folder:
```
pages/leads/
├── index.ts          # Barrel exports
├── types.ts          # Feature-specific types
├── config.ts         # Constants, colors, icons
├── utils.ts          # Helper functions
├── LeadCard.tsx      # Extracted component
├── LeadFilters.tsx   # Extracted component
└── AddLeadDialog.tsx # Extracted component
```

## Component Guidelines
- Use functional components with TypeScript
- Define props interface above component
- Use `export default` for page components
- Use named exports for shared components

```tsx
interface ContactCardProps {
  contact: Contact;
  onEdit: (id: string) => void;
  onDelete: (id: string) => void;
}

export function ContactCard({ contact, onEdit, onDelete }: ContactCardProps) {
  // ...
}
```

## API Layer
- Use `authFetchJson` from `apiClient.ts` for all API calls
- Define types in `types.ts`
- Handle errors gracefully with try/catch
- Use `isUsingRealApi()` for mock fallback

```typescript
export async function getContacts(): Promise<Contact[]> {
  if (!isUsingRealApi()) {
    return mockContacts;
  }
  return authFetchJson<Contact[]>('/api/contacts');
}
```

## State Management
- Use React hooks for local state
- Use TanStack Query for server state (`hooks/queries/`)
- Use Context for global state (OrgContext)

## Styling
- Use Tailwind CSS classes
- Use shadcn/ui components from `components/ui/`
- Follow design system: orange primary, slate neutrals
- Use consistent spacing: `p-4`, `gap-4`, `space-y-4`

## Accessibility
- Include `aria-label` on icon-only buttons
- Use semantic HTML (`main`, `nav`, `section`)
- Set `<main id="main-content">` for skip link
- Ensure keyboard navigation works

## Error Handling
- Wrap pages in `ErrorBoundary`
- Show toast notifications for user actions (sonner)
- Display `EmptyState` for no-data scenarios
- Use `LoadingSpinner` during async operations

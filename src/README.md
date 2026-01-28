# Frontend Workspace (`src/`)

This folder contains the **main SPA** entrypoint and shared assets.

## Key paths
- **`main.tsx`**: React root entry.
- **`app/`**: Main application (routes/pages/components/API client). See `app/README.md`.
- **`styles/`**: Tailwind/theme/css assets used by the app.

## Dev API proxy
During local development, `/api/*` is proxied to the .NET backend (default `http://localhost:5160`) via `vite.config.ts`.


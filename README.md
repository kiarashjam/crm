
# ACI (CRM)

This is a code bundle for Website Structure Overview. The original project is available at https://www.figma.com/design/SLQRDIWbMgPPnEr99EXq1C/Website-Structure-Overview.

## Project report

See `REPORT.md` for a repo-wide overview (what the app does, security/auth notes, and where to find per-folder reports).

## Running the code

Run `npm i` to install the dependencies.

Run `npm run dev` to start the development server.

### Using the real backend

1. Start the backend (see [backend/README.md](./backend/README.md)):  
   `cd backend && dotnet run --project src/ACI.WebApi`
2. Create a `.env` in the repo root (or set in the shell):  
   `VITE_API_URL=http://localhost:5160`  
   (Use the port your API prints, e.g. 5160 or 5161.)
3. Run the frontend: `npm run dev`
4. Open the app, sign in or register with email/password. The app will use the real API for contacts, deals, templates, copy, history, connection, and settings.

Without `VITE_API_URL`, the app uses mock data and the **Try demo (no backend)** button on the login page.

## Publish to GitHub and Azure

See **[DEPLOY.md](./DEPLOY.md)** for: pushing to GitHub, YAML workflows that deploy frontend (Static Web App) and backend (Web App + Azure SQL) on push/release, creating the Azure resource group and all resources, and checking the live site.
  
# ACI (CRM)

**ACI** is a CRM companion app built **from scratch**—it is not a fork or copy of another CRM (e.g. Salesforce, HubSpot, Pipedrive, Zoho, Monday). The frontend (React + TypeScript) and backend (ASP.NET Core) are original; third-party use is limited to attributed libraries (see [ATTRIBUTIONS.md](./ATTRIBUTIONS.md)).

Design and structure reference: [Website Structure Overview](https://www.figma.com/design/SLQRDIWbMgPPnEr99EXq1C/Website-Structure-Overview) (Figma). The linked Figma file is a design reference only; the application code is original.

**The app is standalone:** you can run it entirely on your machine with no required external connection. Use **Try demo (no backend)** for frontend-only with mock data, or run the full stack locally (database + backend + frontend)—see [RUN_FROM_SCRATCH.md](./RUN_FROM_SCRATCH.md) and [LOCAL_DEV.md](./LOCAL_DEV.md).

## Project report

See `REPORT.md` for a repo-wide overview (what the app does, security/auth notes, and where to find per-folder reports).

## Running the code

Run `npm i` to install the dependencies.

Run `npm run dev` to start the development server.

### Database in Docker, frontend and backend locally

To run **only the database** in Docker and the **frontend** and **backend** on your machine, see **[LOCAL_DEV.md](./LOCAL_DEV.md)** (start DB with `docker compose up -d`, then run backend and frontend separately).

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
  
# Local development: database in Docker, frontend and backend on your machine

This describes **standalone** local run: the **database** runs in Docker (or use LocalDB), and the **frontend** and **backend** run on your machine. No external service or cloud is required.

## 1. Start the database (Docker)

From the repo root:

```bash
docker compose up -d
```

This starts SQL Server in a container on port **1433**. The first time can take a minute to pull the image and start.

- **Wait ~15 seconds** after `up -d` before starting the backend so SQL Server is ready.
- To stop: `docker compose down`
- Data is stored in a Docker volume `aci-sqldata` so it persists between restarts.

## 2. Point the backend at the Docker database

Use this connection string for the backend when the DB runs in Docker.

**Option A – environment variable (recommended)**

Before running the backend, set:

- **Windows (PowerShell):**  
  `$env:ConnectionStrings__DefaultConnection = "Server=localhost,1433;Database=ACI;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true"`

- **Windows (CMD):**  
  `set ConnectionStrings__DefaultConnection=Server=localhost,1433;Database=ACI;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true`

- **macOS/Linux:**  
  `export ConnectionStrings__DefaultConnection='Server=localhost,1433;Database=ACI;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true'`

**Option B – config file**

Edit `backend/src/ACI.WebApi/appsettings.Development.json` and set `ConnectionStrings:DefaultConnection` to:

```json
"DefaultConnection": "Server=localhost,1433;Database=ACI;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

(The password must match `MSSQL_SA_PASSWORD` in `docker-compose.yml`.)

## 3. Run the backend

From the repo root:

```bash
cd backend
dotnet run --project src/ACI.WebApi/ACI.WebApi.csproj
```

Or use the **http** profile (port 5160):

```bash
dotnet run --project src/ACI.WebApi/ACI.WebApi.csproj --launch-profile http
```

- API: **http://localhost:5160**
- Swagger: **http://localhost:5160/swagger**

On first run, EF Core applies migrations and seeds data.

## 4. Run the frontend

In a **second terminal**, from the repo root:

1. Create a `.env` (or set in the shell):
   ```bash
   VITE_API_URL=http://localhost:5160
   ```

2. Install and start:
   ```bash
   npm install
   npm run dev
   ```

- App: **http://localhost:5173** (or the port Vite prints)

The frontend will call the real backend. Sign in or register to use contacts, deals, templates, etc.

## Summary

| What        | How                    | URL / port      |
|------------|------------------------|-----------------|
| Database   | `docker compose up -d` | localhost:1433  |
| Backend    | `cd backend && dotnet run --project src/ACI.WebApi` | http://localhost:5160 |
| Frontend   | `npm run dev` + `VITE_API_URL=http://localhost:5160` | http://localhost:5173 |

Without `VITE_API_URL`, the frontend uses mock data and does not need the backend or database.

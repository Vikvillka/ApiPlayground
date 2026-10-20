# ApiPlayground local platform baseline

This repository contains the local React + TypeScript frontend, ASP.NET Core API, PostgreSQL configuration, and a no-dispatch test harness. The frontend is only a platform scaffold; request sending and product request/response UI are not implemented.

## Prerequisites

- .NET 10 SDK
- Node.js 24 and npm
- PostgreSQL 17 installed and running locally (Docker is not required)
- `psql` and `createdb` available on `PATH` for database setup

No public hosting, application secrets, or outbound-request policy is needed for this baseline. Package restore/install requires access to the configured package feeds the first time; application startup and the harness itself do not send outbound HTTP requests.

## Configure PostgreSQL

Start the existing PostgreSQL 17 Windows service if it is stopped:

```powershell
Get-Service postgresql-x64-17
Start-Service postgresql-x64-17
```

Create a local role and database (choose a local password and substitute it below):

```powershell
psql -U postgres -c "CREATE ROLE apiplayground LOGIN PASSWORD 'local-only-change-me';"
createdb -U postgres -O apiplayground apiplayground
```

The API requires runtime configuration; it has no fallback database credential. Set its connection string in the current shell, replacing the local password:

```powershell
$env:ConnectionStrings__Postgres = "Host=localhost;Port=5432;Database=apiplayground;Username=apiplayground;Password=your-local-password"
```

Do not commit local credentials or put them in `.env` files. Supply this setting to the API process and EF commands whenever they need a database connection.

## Install and initialize

From the repository root:

```powershell
Push-Location frontend
npm ci
Pop-Location
Push-Location backend
dotnet tool restore
Pop-Location
dotnet restore backend/ApiPlayground.sln
Push-Location backend
dotnet ef database update --project src/ApiPlayground.Infrastructure --startup-project src/ApiPlayground.Api
Pop-Location
```

Infrastructure owns the single EF Core migration assembly and history stream. The initial migration establishes that baseline under its original ID. It intentionally contains no product tables; later approved stories add application data models. Applying it again is safe.

## Run locally

Run the API in one PowerShell terminal:

```powershell
dotnet run --project backend/src/ApiPlayground.Api --urls http://localhost:5000
```

Verify its local health endpoint in another terminal:

```powershell
Invoke-RestMethod http://localhost:5000/health
```

Run the frontend in a separate terminal:

```powershell
Push-Location frontend
npm run dev -- --host 127.0.0.1
```

Open the local Vite address printed in the terminal (normally `http://127.0.0.1:5173`). The scaffold does not call the API or dispatch HTTP requests.

## Build and test

```powershell
Push-Location frontend
npm run build
Pop-Location
dotnet build backend/ApiPlayground.sln
```

The API health test uses ASP.NET Core's in-process test host. The controlled-target self-test verifies readiness and a zero dispatch count without calling its handler. Neither test contacts an external service. PostgreSQL migration tests require a disposable test-admin connection and fail rather than skip when it is missing or unavailable. Configure a local role permitted to create/drop databases against the PostgreSQL maintenance database (never the application database):

```powershell
$env:APIPLAYGROUND_TEST_ADMIN_CONNECTION = "Host=localhost;Port=5432;Database=postgres;Username=your-test-admin;Password=your-local-password"
Push-Location frontend
npm test
Pop-Location
dotnet test backend/ApiPlayground.sln --configuration Release
```

Tests only create/drop databases with generated `apg_mig_` names. CI provisions PostgreSQL 17 and passes an ephemeral service connection.

## Troubleshooting

- **PostgreSQL connection refused:** confirm the local PostgreSQL 17 service is running and listening on port 5432 (`pg_isready -h localhost -p 5432`).
- **Password authentication failed:** set `ConnectionStrings__Postgres` in the shell running EF commands and the API, matching the local role's password.
- **Database or role already exists:** keep the existing local objects and skip their creation; then apply the migration again.
- **`dotnet ef` is unavailable:** run `dotnet tool restore` from the `backend` directory.
- **Migration tests cannot connect:** set `APIPLAYGROUND_TEST_ADMIN_CONNECTION` to a PostgreSQL maintenance database and verify the role can create and drop databases; never point it at `apiplayground`.
- **Build/test package restore fails:** verify access to the configured NuGet/npm package feeds, then rerun restore and the failing command.
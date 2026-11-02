<!-- bmad:context -->
<!-- Verified 2026-11-02 against 6d5ae20040fd50a77cfe5eed586ad4662a410e7b. Managed by bmad-project-context; edits inside this block are replaced on refresh. Keep anything you want preserved outside the markers. -->

## ApiPlayground

Local-only API playground: ASP.NET Core on .NET 10 with EF Core and PostgreSQL 17 in `backend/`, React and TypeScript with Vite, MobX and MUI in `frontend/`. Planning and tickets live in `_bmad-output/`.

## Policy

- Do not modify a plan section marked `<frozen-after-approval>` unless the user renegotiates it.
- Do not change environment settings (databases, services, roles, target machine) without asking the user first.

## Where things are

- Architecture spine: `_bmad-output/initiative-apiplayground-mvp/architecture-apiplayground-backend/architecture-apiplayground-backend.md`
- Ticket status: `uv run _bmad/method/scripts/tickets.py --project-root . status`
- Local setup, PostgreSQL, and environment keys: `README.md`

## Running and verifying

- Frontend, from `frontend/`: `npm ci`, `npm run build`, `npm test`. E2E: `npx playwright install chromium` once, then `npm run test:e2e`.
- Backend: `dotnet build backend/ApiPlayground.sln`, then `dotnet test backend/ApiPlayground.sln --configuration Release`.
- Run `dotnet tool restore` in `backend/` before using `dotnet ef`.
- Export `ConnectionStrings__Postgres` in the shell before running the API or EF commands. Neither reads `.env`.
- Create EF migrations only with `dotnet ef migrations add`, never by hand.
- `APIPLAYGROUND_TEST_ADMIN_CONNECTION` must target database `postgres`, never `apiplayground`. Never run broad prefix cleanup of databases or roles.

## Conventions that differ from defaults

- All target HTTP dispatch goes through the single application port and its guarded Infrastructure adapter. Do not add another outbound request path.
- The API binds to loopback only. Never expose it to the LAN, and keep the Host, Origin, and header checks on the tracer route.
- The API project is the only composition root. Dependencies run inward: Domain has no project references, Application references Domain, Infrastructure references Application and Domain, Api references Application and Infrastructure.
- Routes are controllers, not Minimal APIs.

## Known pitfalls

- Agents retried endlessly when a connection string was missing and stalled the task. Stop and ask the user for it.
- Agents changed environment settings without discussing them first. Ask before changing them.
- Agents deviated from the declared architecture. Read the architecture spine before adding a project or reference.

<!-- /bmad:context -->

# Stack and Version Claims Review

**Reviewed:** 2026-10-19
**Scope:** Version/support/compatibility statements in the architecture spine's Stack table, checked against the project files and live official documentation. This review does not change the spine.

## Verdict

**Mostly accurate, with stale patch-level statements and two wording/verification caveats.** The supported major-version choices are sound: .NET 10 and EF Core 10 are LTS, EF Core 10 requires .NET 10, Npgsql EF provider 10 is the matching EF 10 provider line, and PostgreSQL 17 remains supported. The repo explicitly pins the API's target framework and EF/Npgsql package versions; it does not pin the SDK or PostgreSQL minor. At review time, both local observed patch versions (runtime 10.0.10 and PostgreSQL 17.10) are behind current official patch/minor releases. In particular, describing SDK 10.0.302 as locally observed is true, but it is not the installed runtime version and is not repository-pinned.

## Findings

1. **.NET 10 LTS support — verified; local patch is behind.** Microsoft lists .NET 10 as LTS, with support through **November 14, 2028**. The repository's API and test projects target `net10.0`; there is no `global.json`, so the SDK is not pinned in the repository. Local `dotnet --list-sdks` / `dotnet --version` report SDK **10.0.302**, while `dotnet --list-runtimes` reports `Microsoft.NETCore.App` and `Microsoft.AspNetCore.App` **10.0.10**. Microsoft lists .NET **10.0.13** as the latest servicing release on the review date and SDK **10.0.402** as current. Thus the spine's `10.0.302` is correctly labeled an observed SDK, not a repo pin, but it is not a current servicing-level claim; the runtime patch also differs from the SDK patch and is behind current.

   Sources: [Microsoft .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), [Microsoft .NET 10 downloads](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).

2. **EF Core 10 LTS and .NET requirement — verified; pinned package patch is old.** Microsoft states EF Core 10 targets .NET 10, is supported through **November 10, 2028**, and requires the .NET 10 SDK to build and .NET 10 runtime to run (not earlier .NET versions). The API project pins `Microsoft.EntityFrameworkCore.Design` **10.0.0** in `backend/src/ApiPlayground.Api/ApiPlayground.Api.csproj`; the resolved graph also contains EF Core and Relational **10.0.0**. EF Core's official release page advises using the latest patch of a supported release. NuGet's current stable Design package is **10.0.13**, so the spine's “EF Core 10 is LTS” claim is correct, but the repository-pinned 10.0.0 is not the current patch.

   Sources: [EF Core release/support table and patch guidance](https://learn.microsoft.com/en-us/ef/core/what-is-new/), [EF Core 10 release notes and .NET requirement](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew), [Microsoft EF Design 10.0.13 package](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Design/10.0.13).

3. **Npgsql provider compatibility with EF 10 — verified for the selected major line; package pin is behind current.** `backend/src/ApiPlayground.Api/ApiPlayground.Api.csproj` pins `Npgsql.EntityFrameworkCore.PostgreSQL` **10.0.0**. The successful restore/build and resolved assets show provider, EF Core, and EF Core Relational all at **10.0.0**; tests also pass. Independently, Npgsql's official EF provider 10.0 release notes document EF 10 feature support, and the provider's tagged 10.0.0 source sets EF dependencies to the `[10.0.0,11.0.0)` range, confirming intended compatibility with EF Core 10. NuGet currently lists provider **10.0.3**, targeting .NET 10. Therefore “build-verified with EF Core 10” is accurate for this checkout and exact package set, but the current pin is not the latest provider patch. A successful build validates compilation/restore, not every runtime/database behavior. The solution test run passed **3/3** tests; the suite does not establish full provider compatibility coverage.

   Sources: [Npgsql EF provider 10.0 release notes](https://www.npgsql.org/efcore/release-notes/10.0.html), [provider v10.0.0 dependency version policy](https://github.com/npgsql/efcore.pg/blob/v10.0.0/Directory.Packages.props), [NuGet provider 10.0.3](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/10.0.3).

4. **PostgreSQL 17 support/minor recommendation — major is current; minor statement and local observation need distinction.** The PostgreSQL Global Development Group's live versioning page lists PostgreSQL 17 as supported through **November 8, 2029**, with current minor **17.11**, and explicitly recommends always running the current minor for a major version. The repo requires major 17 (e.g. `README.md` and the architecture story), but has no minor-version pin. Local `postgres.exe --version` and `pg_config --version` report **17.10**, and Windows service `postgresql-x64-17` is running. This confirms the installed server binaries, not by itself the exact version serving a connection: querying the server required credentials and was not completed. The spine's “17.10 installed” is consistent with observed binaries; “17.11 is current” is correct and flags the installed minor as behind. PostgreSQL 17.11 is a current minor, not a required repo pin.

   Source: [PostgreSQL official versioning policy](https://www.postgresql.org/support/versioning/).

## Repository and local verification

- API target: `backend/src/ApiPlayground.Api/ApiPlayground.Api.csproj` → `net10.0`.
- Test target: `backend/tests/ApiPlayground.Tests/ApiPlayground.Tests.csproj` → `net10.0`.
- Direct package pins: EF Design **10.0.0** and Npgsql EF provider **10.0.0** in the API project; test host package is **10.0.0** in the test project.
- No root or backend `global.json` found. No PostgreSQL minor pin found; project/docs specify major 17.
- Local observations: SDK **10.0.302**; .NET and ASP.NET Core runtimes **10.0.10**; PostgreSQL 17 service running and installed binaries **17.10**.
- `dotnet build backend/ApiPlayground.sln --no-restore`: succeeded, with three apphost copy retry warnings because the running `ApiPlayground.Api` process held its output executable open.
- `dotnet test backend/ApiPlayground.sln --no-build`: succeeded, **3 passed, 0 failed**.
- `dotnet ef --version`: unavailable as a command/tool in this environment. This does not invalidate restore/build compatibility, but EF CLI tooling itself was not verified. PostgreSQL server SQL version was not queried because the local `postgres` account requested a password.

## Bottom line

The architecture's major-version choices and support claims are current and substantiated. The items to treat as stale are the pinned EF/Npgsql **10.0.0** package patches and the locally installed .NET runtime **10.0.10** / PostgreSQL **17.10** versus current official servicing/minor levels. The SDK and PostgreSQL minor are observed machine state, not repository-pinned versions. No claim that the SDK or PostgreSQL minor is repo-pinned is supported by the checked-in files.

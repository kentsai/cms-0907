# CMS

.NET 9 Web API + Dapper (`src\CMS.API`, xUnit tests in `src\CMS.API.Tests`) and Angular 20 +
PrimeNG Aura (`src\CMS.NG`), scaffolded from the SQL Server schema in `database\*.sql`.
Feature specs: `spec\{sub-system}\{Table}.md`; login / JWT / profile / change-password flows are
specified in `spec\auth\Auth.md`. New features: run `/crud`.

## Commands (API and frontend in separate terminals, no proxy)

```powershell
dotnet run --project C:\dev\cms\src\CMS.API          # http://localhost:5000, /swagger
cd C:\dev\cms\src\CMS.NG; npm start                   # http://localhost:4200
dotnet test C:\dev\cms\src\CMS.sln
cd C:\dev\cms\src\CMS.NG; npx ng test --watch=false --browsers=ChromeHeadless
```

## Rules (details in `docs\claude\environment.md`)

- Dapper only, no EF; everything async with `CancellationToken`.
- Standalone Angular components; tests stay on Karma + Jasmine.
- Pinned: Swashbuckle 7.2.0 (never `Microsoft.AspNetCore.OpenApi`), `@angular/animations`
  20.3.x explicit; never `--legacy-peer-deps`.
- Files with Chinese text: edit with Write/Edit tools, never Bash `perl`/heredocs.
- No local `CMS` database: unit tests pass, live data calls fail.
- MSB3027 on `CMS.API.exe` = API running; build with `-p:ArtifactsPath=<tmp>` instead of killing it.
- Every API action except `POST /api/auth/login` needs a Bearer JWT (global filter, no role checks);
  `PasswordHash` never crosses the API, and a password change revokes older tokens.

## Reference notes (read on demand)

| `docs\claude\…` | When |
|---|---|
| `feature-infrastructure.md` | Repo layout; building/changing a CRUD feature, lookup, audit, shell menu, TestBed |
| `feature-status.md` | What is built (with test totals), non-obvious feature behaviour, next tables |
| `environment.md` | A build, install, shell, or tool misbehaves |

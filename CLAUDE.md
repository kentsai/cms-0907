# CMS

.NET 9 Web API + Dapper (`src\CMS.API`, xUnit tests in `src\CMS.API.Tests`) and Angular 20 +
PrimeNG Aura (`src\CMS.NG`), scaffolded from the SQL Server schema in `database\*.sql`.
Feature specs: `spec\{sub-system}\{Table}.md`; login / JWT / profile / change-password flows are
specified in `spec\auth\Auth.md`. Approved implementation plans (design decisions, with the commit that
built them) live in `plans\YYYY-MM-DD-<feature>.md`. New features: run `/crud`.

## Commands (API and frontend in separate terminals, no proxy)

```powershell
dotnet run --project C:\dev\cms\src\CMS.API          # http://localhost:5000, /swagger
cd C:\dev\cms\src\CMS.NG; npm start                   # http://localhost:4200
dotnet test C:\dev\cms\src\CMS.sln
cd C:\dev\cms\src\CMS.NG; npx ng test --watch=false --browsers=ChromeHeadless
```

## Deploy to IIS (`deploy\`, runbook `DEPLOY-IIS.md`)

```powershell
cd C:\dev\cms\deploy                                  # elevated PowerShell
.\setup-iis.ps1 -GrantSqlAccess                       # once: IIS + Hosting Bundle + URL Rewrite + ARR, pools, sites, SQL login
.\deploy.ps1                                          # every time: publish API + build NG, stamp web.config, cycle pool, copy
.\deploy.ps1 -ApiOnly | -NgOnly | -SkipBuild
```

Two IIS sites: `CMS` (:80, `C:\VHome\CMS\NG`, the Angular dist) reverse-proxies `/api/*` via URL Rewrite + ARR
to `CMS.API` (:5001, `C:\VHome\CMS\API`, AspNetCoreModuleV2 in-process). Production `environment.ts` therefore
has `apiBaseUrl: '/api'` (same-origin); only `environment.development.ts` points at `http://localhost:5000/api`.
Both `web.config`s are stamped from `deploy\{CMS.API,CMS.NG}\web.config.template` at deploy time, never
committed; the connection string and `ASPNETCORE_ENVIRONMENT` are injected there (CONFIG block at the top of
`deploy.ps1`). It stamps `ASPNETCORE_ENVIRONMENT=Production`: no Swagger on a deployed site (Development-only);
smoke-test with `GET :5001/api/publish-statuses` (401 without a token = up).
`deploy\publish\` is build output (gitignored). The `CMS` database and its `SysConfig.appConfig` row must exist.

## Rules (details in `docs\claude\environment.md`)

- Dapper only, no EF; everything async with `CancellationToken`.
- Standalone Angular components; tests stay on Karma + Jasmine.
- Pinned: Swashbuckle 7.2.0 (never `Microsoft.AspNetCore.OpenApi`), `@angular/animations`
  20.3.x explicit; never `--legacy-peer-deps`.
- Files with Chinese text: edit with Write/Edit tools, never Bash `perl`/heredocs.
- No local `CMS` database: unit tests pass, live data calls fail.
- MSB3027 on `CMS.API.exe` = API running; build with `-p:ArtifactsPath=<tmp>` instead of killing it.
- Every API action except `POST /api/auth/login` needs a Bearer JWT (global filter). The account / role
  endpoints (`AppUsersController`, `AppRolesController`, the `app-users` + `app-roles` lookups) additionally
  require the `Admin` role via `[Authorize(Policy = AuthorizationPolicies.Admin)]` — 403 otherwise; the SPA
  mirrors it with `adminGuard`. `PasswordHash` never crosses the API, and a password change revokes older
  tokens. A login with the default password gets a token that only opens `change-password` (403 elsewhere;
  SPA route `/change-password`).
- Passwords are salted PBKDF2 (`PasswordHasher.Hash` / `.Verify`); legacy unsalted SHA-256 rows still verify
  and are rewritten in place on the owner's next login. Never store `Sha256Hex` output.
- Swagger is Development-only (`app.Environment.IsDevelopment()` in `Program.cs`): `dotnet run` has it at
  `:5000/swagger`, a deployed site never does. Never stamp `Development` on a server to get it back.
- Security headers: the API sets its own on every response (`Infrastructure\SecurityHeadersMiddleware`, first in
  the pipeline, `no-store` + `default-src 'none'`; `/swagger` gets a looser policy so the UI runs). The SPA's set
  lives in `deploy\CMS.NG\web.config.template`. Its CSP has **no `'unsafe-inline'` for scripts**, which is why
  `angular.json` sets `optimization.styles.inlineCritical: false` — the critical-CSS inliner emits an inline
  `onload=` handler that the policy blocks. Don't re-enable it, and don't add inline `<script>` to `index.html`.
- Chromeless routes: a route with `data: { chromeless: true }` (the course print view
  `/course/courses/:id/print`) renders only the outlet, no shell / toast / confirm dialog (`app.ts`).

## Reference notes (read on demand)

| File | When |
|---|---|
| `docs\claude\feature-infrastructure.md` | Repo layout; building/changing a CRUD feature, lookup, audit, shell menu, TestBed |
| `docs\claude\feature-status.md` | What is built (with test totals), non-obvious feature behaviour, next tables |
| `docs\claude\environment.md` | A build, install, shell, or tool misbehaves; what this box has (and lacks) for an IIS deploy |
| `DEPLOY-IIS.md` | Deploying to IIS: topology, one-time setup, remote servers, troubleshooting table |

## gstack

gstack (`~/.claude/skills/gstack`) is installed. Use the `/browse` skill from gstack for all web
browsing; never use `mcp__claude-in-chrome__*` tools. Available skills: `/office-hours`,
`/plan-ceo-review`, `/plan-eng-review`, `/plan-design-review`, `/design-consultation`,
`/design-shotgun`, `/design-html`, `/review`, `/ship`, `/land-and-deploy`, `/canary`, `/benchmark`,
`/browse`, `/connect-chrome`, `/qa`, `/qa-only`, `/design-review`, `/setup-browser-cookies`,
`/setup-deploy`, `/setup-gbrain`, `/retro`, `/investigate`, `/document-release`,
`/document-generate`, `/codex`, `/cso`, `/autoplan`, `/plan-devex-review`, `/devex-review`,
`/careful`, `/freeze`, `/guard`, `/unfreeze`, `/gstack-upgrade`, `/learn`.

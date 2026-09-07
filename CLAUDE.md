# CMS

Full-stack CMS scaffolded from an existing SQL Server database schema.
Backend: .NET 9 Web API + Dapper. Frontend: Angular 20 + PrimeNG.

## Layout

```
database\              *.sql schema files (source of truth for the data model)
spec\                  code-gen.convention.md, ui-sample-*.png
src\
  global.json          pins the SDK to 9.0.317 (machine also has 10.0.400)
  CMS.sln
  CMS.API\             .NET 9 Web API, controllers + Dapper
  CMS.API.Tests\       xUnit + Moq, references CMS.API
  CMS.NG\              Angular 20, standalone components, PrimeNG
```

## Commands

Run the API and the frontend in **separate terminals** — the frontend calls the API
directly and there is no dev-server proxy.

```powershell
# API -> http://localhost:5000, Swagger UI at /swagger
dotnet run --project C:\dev\cms\src\CMS.API

# Frontend -> http://localhost:4200
cd C:\dev\cms\src\CMS.NG; npm start

# Tests
dotnet test C:\dev\cms\src\CMS.sln
cd C:\dev\cms\src\CMS.NG; npm test                       # interactive (Karma + Jasmine)
cd C:\dev\cms\src\CMS.NG; npx ng test --watch=false --browsers=ChromeHeadless
```

## Backend conventions

- **Dapper only — no Entity Framework.** Repositories take an `IDbConnectionFactory`
  (`Infrastructure\`) and call `CreateOpenConnectionAsync`.
- **All API actions and data access are async**, with `CancellationToken` flowed through.
- Connection string lives under `ConnectionStrings:CMS` in `appsettings.json`
  (`Server=.\SQLEXPRESS;Database=CMS;Trusted_Connection=True;...`).
- Swagger is **Swashbuckle 7.2.0**, pinned by request. The .NET 9 template ships
  `Microsoft.AspNetCore.OpenApi`/`AddOpenApi` instead — that package was deliberately
  removed. Don't reintroduce it; use `AddSwaggerGen`/`UseSwagger`/`UseSwaggerUI`.
- Swagger UI is enabled in **all** environments, not just Development.
- CORS policy `LocalhostCorsPolicy` allows any **loopback** origin (`uri.IsLoopback`),
  so any localhost port works without editing a whitelist.
- `Program.cs` ends with `public partial class Program;` so tests can reference the
  entry-point assembly.
- There is no `UseHttpsRedirection` — the app is HTTP-only on port 5000 by design.

## Frontend conventions

- **Standalone components**; no NgModules.
- **No proxy.** The API base URL comes from `src\environments\environment*.ts`
  (`apiBaseUrl`). `environment.ts` is production; `environment.development.ts` replaces
  it via `fileReplacements` in the development build configuration.
- Path aliases in `tsconfig.json`: `@environments/*`, `@app/*`, `@core/*`, `@features/*`.
- PrimeNG is configured in `app.config.ts` via `providePrimeNG` with the **Aura** preset,
  alongside `provideAnimationsAsync()` and `provideHttpClient(withFetch())`.
  `primeicons.css` is loaded from `angular.json` `styles`, not imported in SCSS.
- Test setup is the Angular default (**Karma + Jasmine**) — keep it; do not migrate to
  Vitest/Jest.
- Components using the app shell need `provideRouter([])` and `provideNoopAnimations()`
  in `TestBed`, or PrimeNG animations will throw.

### App shell

`src\app\app.ts` is the shell: topbar + collapsible sidebar (CSS grid) + `router-outlet`.
The sidebar is a PrimeNG `PanelMenu` driven by the `menuItems` array. Add new features as
entries there.

Current menu: `系統管理 Admin` → `角色 AppRole` → routes to `/admin/app-roles`.

## Environment gotchas

These cost real time in a previous session — check them before debugging further.

- **PowerShell execution policy.** All persistent scopes were `Undefined`, so Windows fell
  back to `Restricted` and refused to load `npm.ps1` (`npm start` failed with
  `UnauthorizedAccess`). Fixed with
  `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`. If it recurs, `npm.cmd start`
  works without changing policy.
- **`@angular/animations` must stay on 20.3.x.** npm otherwise resolves it to 20.1.8 to
  satisfy PrimeNG's peer range, which pins `@angular/common` to that exact version and
  breaks the install with `ERESOLVE`. It is an explicit dependency for this reason —
  don't "clean it up". Do **not** paper over this with `--legacy-peer-deps`.
- **Two SDKs are installed** (9.0.317 and 10.0.400). `global.json` forces 9.0.317. Without
  it, packages resolve to net10.0-only builds — this is why
  `Microsoft.AspNetCore.Mvc.Testing` could not be added (latest is net10.0-only). Pin the
  version if that package is ever needed.
- Node lives at `C:\Program Files\nodejs`; it is not always on `PATH` in non-interactive
  shells.

## Status

Scaffolding is complete and verified: solution builds with 0 warnings, API returns 200 on
`/swagger`, `ng build` succeeds, and 4 Angular tests pass.

**Not yet built — blocked on missing inputs.** `database\` and `spec\` are empty. The
first feature (AppRole CRUD: list with query filter, view, edit, add, plus xUnit and
Angular tests) needs `database\auth.sql` for the real column names, and
`spec\code-gen.convention.md` for naming/layering. The shell's `/admin/app-roles` link
currently has no route behind it. The shell styling is a conventional admin layout, not
derived from `spec\ui-sample-*.png`, which were never supplied.

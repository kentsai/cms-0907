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

Current menu: `系統管理 Admin` → `角色 AppRole` (`/admin/app-roles`), `使用者 AppUser`
(`/admin/app-users`), `發布狀態 PublishStatus` (`/admin/publish-statuses`);
`課程管理 Course` → `合作夥伴 Partner` (`/course/partners`), `課程群組 CourseGroup`
(`/course/course-groups`), `課程 Course` (`/course/courses`).

`<p-toast/>` and `<p-confirmdialog/>` are rendered once in `app.html`; `MessageService`
and `ConfirmationService` are provided app-wide in `app.config.ts`. Feature pages only
inject them. Any TestBed that mounts `App` or a feature page must provide both.

### Shared feature infrastructure (added with PublishStatus)

- `Infrastructure\RowAuditWriter.cs` (`IRowAuditWriter`) writes `dbo.RowAudit` on the
  caller's connection/transaction. User name falls back to `"system"` (no auth yet).
- `Infrastructure\AuditHelper.ChangedColumns` diffs a model against a request by property
  name for UPDATE audit descriptions.
- `Infrastructure\EntityInUseException` — repositories throw it on SQL error 547 (FK
  violation); controllers return 409.
- `Controllers\LookupsController` (`/api/lookups/*`) and `core/services/lookup.service.ts`
  — add one action/method per FK-target table. Tables with their own repository expose
  `GetLookupAsync` there; `LookupRepository` holds only lookups for tables that have **no**
  feature yet (currently `certifications`, `job-categories`) — move each one out when its
  feature is generated.
- `Controllers\RowAuditsController` (`GET /api/row-audits/{table}/{pk}`) feeds
  `core/components/row-audit-badge` shown in detail/edit toolbars.
- `core/utils/session-storage.util.ts` — guarded read/write for `{entity}-list-*` keys.
- `core/utils/date.util.ts` — `toIso` / `fromIso` / `addYears` for `date` columns (local
  components only, never `toISOString`). Dapper 2.1.79 maps `DateOnly` natively, so no
  type handler is registered in `Program.cs`.
- `core/utils/ascii.validator.ts` — `asciiValidator` for `varchar` columns
  (`partner.model.ts` re-exports it for older imports).
- `Infrastructure\PasswordHasher` (SHA-256 → lowercase hex) and
  `Infrastructure\AppConfigJson` (reads `defaultPassword` out of the `SysConfig.appConfig`
  JSON; throws `AppConfigException` → controllers return 500).

Feature specs live in `spec\{sub-system}\{Table}.md`; generate/build them with `/crud`.

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
- **Do not edit files containing Chinese text through Bash `perl`/heredocs.** A Bash
  heredoc holding CJK content failed to parse (`unexpected EOF while looking for matching
  quote`), and `perl -pi` with a `\x{...}` replacement upgraded the whole file to wide
  chars and **double-encoded every existing Chinese string** (`草稿` → `èç¨¿`). Use the
  Write/Edit tools for any file with non-ASCII content; `perl` is fine for ASCII-only edits.
- **`winget` is unreliable here — don't install with it.** `winget install` hung
  indefinitely (10+ min at ~0.9s CPU, no installer child process, nothing installed) and
  had to be killed. Separately, `winget list` aborts with `0x8a150042` because the
  `msstore` source demands an interactive agreement prompt that a non-interactive shell
  cannot answer; `--accept-source-agreements` does not suppress it. Download vendor
  installers directly instead — e.g. VS Code from
  `https://update.code.visualstudio.com/latest/win32-x64-user/stable`, then run it with
  `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART` (add `/MERGETASKS=...,addtopath`).
- **VS Code lives at `%LOCALAPPDATA%\Programs\Microsoft VS Code` (1.136.1).** It was
  previously a user install on `D:\Microsoft VS Code`; that copy was uninstalled. `code`
  is on `PATH` via `bin\code.cmd`.
- **A commit-hash subfolder in the VS Code install root is normal, not corruption.** The
  install root holds only `Code.exe`, `bin\`, `unins000.*` and a folder named for the
  build commit (e.g. `a44adf7f53`) that contains `resources\`, `locales\` and the DLLs.
  Several such folders accumulate across updates. A clean install from Microsoft's signed
  installer produces exactly this shape — do not diagnose it as a broken/half-updated
  install.

## Status

Scaffolding is complete and verified: solution builds with 0 warnings, `ng build` succeeds.

**Built:** `PublishStatus` CRUD (spec at `spec\admin\PublishStatus.md`) — API, Angular
list/detail/form, lookup endpoint, RowAudit plumbing, 19 xUnit + 31 Karma tests passing.
Committed and pushed to `origin/develop` (`9f2fd4c`).

**Built:** `AppRole` CRUD (spec at `spec\admin\AppRole.md`) — string PK `RoleId` (routes
use `{id}` with no `:int`; the service URL-encodes it), `pkid` is display-only, N-N with
`AppUser` via `AppUserRole` managed by a `p-multiselect` (delete-then-reinsert in the same
transaction). Adds `StringLookupItem`, `ILookupRepository`/`LookupRepository` (app-users
lookup), and `/api/lookups/app-roles` + `app-users`. Totals now 39 xUnit + 55 Karma tests.
Committed together with `Partner` (see below).

**Built:** `Partner` CRUD (spec at `spec\course\Partner.md`) — smallint IDENTITY PK, no FKs,
five inbound references (Course, Certification, PartnerCourseGroup, Seminar, Promotion2) shown
as link buttons. `AppKey` / `ImageFilename` are `varchar`, so both sides enforce printable-ASCII.
`PartnerCourseGroup` is treated as a child entity, not an N-N junction (it has its own
columns). Adds `/api/lookups/partners`. First entry in the new `課程管理 Course` menu group.
Totals now 66 xUnit + 83 Karma tests. Committed and pushed to `origin/develop`.

**Built:** `CourseGroup` CRUD (spec at `spec\course\CourseGroup.md`) — smallint IDENTITY PK,
one `Description` column, referenced by `Course` and `PartnerCourseGroup` (link buttons).
Adds `/api/lookups/course-groups`. Default sort `pkid DESC` (no DisplayOrder column).

**Built:** `Course` CRUD (spec at `spec\course\Course.md`) — int IDENTITY PK; FKs to
Partner / CourseGroup (nullable) / PublishStatus are **JOINed** so every row carries
`partnerName`, `courseGroupDescription`, `publishStatusDescription` (the list shows those
labels, per the requested column set); N-N `CourseInCertification` + `CourseJobCategories`
via two `p-multiselect`s (delete-then-reinsert; both junctions `ON DELETE CASCADE`);
`date` columns as `DateOnly` with `p-datepicker`; `ScheduleOff` auto-defaults to
`ScheduleOn + 10y`. Adds `/api/lookups/courses`, `certifications`, `job-categories`.
Deliberately **not** built from the sample spec: the `/copy` action, QR code, print-to-PDF
and the CourseRelatedLink / CourseRecomm sub-panels; `ClassSection` is not in the schema.

**Built:** `AppUser` CRUD (spec at `spec\auth\AppUser.md`) — string PK `UserId`, N-N with
`AppRole` via `AppUserRole`. **`PasswordHash` never crosses the API**: create seeds it with
SHA-256 of `SysConfig.appConfig.defaultPassword`, update never touches it, and
`POST /api/app-users/{id}/reset-password` re-applies the default (detail page has a 重設密碼
button). The `app-users` lookup moved from `LookupRepository` into `AppUserRepository`.
The hex hash format is an assumption — no legacy hashes were available to compare.

Totals now **149 xUnit + 176 Karma** tests passing; `ng build` succeeds (initial bundle
exceeds the 500 kB budget warning — PrimeNG shared chunks — not an error).
CourseGroup / Course / AppUser are **uncommitted** in the working tree as of 2026-09-07.

**Not yet built:** everything else in `course.sql` / `promotion.sql` (Certification,
JobCategory, CourseFAQ, CourseRelatedLink, HotCourse, CourseRecomm, LinkDefinition,
PartnerCourseGroup, TrainingCenter, Seminar, Promotion2, FeaturedPromoItem). Certification
and JobCategory lookups are already served from `LookupRepository`.

**Build gotcha:** if `dotnet build` fails with MSB3027 on `CMS.API.exe`, the API is
running from `bin\` (e.g. `dotnet run` in another terminal). Don't kill it blindly —
build/test with `-p:ArtifactsPath=<some tmp dir>` to bypass the locked folder.

**Shell tooling note:** `python` is not installed; the `python` command resolves to the
Windows Store alias and hangs a non-interactive shell. Use the Edit/Write tools or `perl`.

**Local DB caveat:** `.\SQLEXPRESS` is running but has **no `CMS` database** (only the
four system DBs). The API compiles and serves Swagger, but every data endpoint will fail
until the database is created from `database\*.sql`.

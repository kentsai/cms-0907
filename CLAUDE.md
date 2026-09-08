# CMS

Full-stack CMS scaffolded from an existing SQL Server schema.
Backend: .NET 9 Web API + Dapper. Frontend: Angular 20 + PrimeNG (Aura).

## Layout

```
database\        *.sql schema files (source of truth for the data model)
spec\            code-gen.convention.md, feature specs in spec\{sub-system}\{Table}.md
docs\claude\     reference notes for Claude (read on demand, see below)
src\
  global.json    pins the SDK to 9.0.317
  CMS.API\       .NET 9 Web API, controllers + Dapper
  CMS.API.Tests\ xUnit + Moq
  CMS.NG\        Angular 20, standalone components, PrimeNG
```

## Commands

API and frontend run in **separate terminals**; there is no dev-server proxy.

```powershell
dotnet run --project C:\dev\cms\src\CMS.API          # http://localhost:5000, /swagger
cd C:\dev\cms\src\CMS.NG; npm start                   # http://localhost:4200
dotnet test C:\dev\cms\src\CMS.sln
cd C:\dev\cms\src\CMS.NG; npx ng test --watch=false --browsers=ChromeHeadless
```

## Core rules

- **Dapper only, no Entity Framework.** All actions and data access are async with
  `CancellationToken`.
- **Standalone Angular components, no NgModules.** Tests stay on Karma + Jasmine.
- **Swagger is Swashbuckle 7.2.0**, pinned. Never add `Microsoft.AspNetCore.OpenApi`.
- **`@angular/animations` stays on 20.3.x** and is an explicit dependency. Never use
  `--legacy-peer-deps`.
- **Never edit files with Chinese text via Bash `perl`/heredocs** — it double-encodes
  CJK. Use the Write/Edit tools.
- **No local `CMS` database exists.** Unit tests pass; live data endpoints fail.
- **MSB3027 on `CMS.API.exe`** means the API is running. Build/test with
  `-p:ArtifactsPath=<tmp dir>` instead of killing it.
- New features: run `/crud` (generates the spec, then scaffolds API + Angular + tests).

## Reference notes — read when relevant

| Read | When |
|---|---|
| `docs\claude\feature-infrastructure.md` | Building or changing a CRUD feature, lookup, audit, app shell menu, or TestBed setup |
| `docs\claude\feature-status.md` | Picking the next table, or touching a built feature's non-obvious behaviour (e.g. AppUser password rules) |
| `docs\claude\environment.md` | A build, install, shell, or tool misbehaves (SDKs, Node PATH, execution policy, winget, VS Code) |

## Status

Built: PublishStatus, AppRole, AppUser, Partner, CourseGroup, Course, Certification, and the
custom FeaturedPromoItem weekly board (`首頁 Home` menu). Course detail also shows a
downloadable QR code (`core/components/qr-code`, `qrcode` npm package), and the Course list
edits cells in place on double-click (`course-list/course-inline-edit.ts`). 207 xUnit + 272 Karma
tests pass. Remaining tables are listed in `docs\claude\feature-status.md`.

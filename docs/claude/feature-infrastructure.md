# Shared feature infrastructure

Read this when building or modifying a CRUD feature (`/crud`), a lookup, or audit logging.
Overview and always-on conventions are in `CLAUDE.md`.

## Backend (`src\CMS.API`)

- `Infrastructure\IDbConnectionFactory` — repositories call `CreateOpenConnectionAsync`.
  Dapper only, no EF. Everything async with `CancellationToken` flowed through.
- `Infrastructure\RowAuditWriter.cs` (`IRowAuditWriter`) writes `dbo.RowAudit` on the
  caller's connection/transaction. User name falls back to `"system"` (no auth yet).
- `Infrastructure\AuditHelper.ChangedColumns` diffs a model against a request by property
  name for UPDATE audit descriptions.
- `Infrastructure\EntityInUseException` — repositories throw it on SQL error 547 (FK
  violation); controllers return 409.
- `Infrastructure\PasswordHasher` (SHA-256 → lowercase hex) and
  `Infrastructure\AppConfigJson` (reads `defaultPassword` out of the `SysConfig.appConfig`
  JSON; throws `AppConfigException` → controllers return 500).
- `Controllers\LookupsController` (`/api/lookups/*`) — one action per FK-target table.
  Tables with their own repository expose `GetLookupAsync` there; `LookupRepository`
  holds only lookups for tables with **no** feature yet (currently `job-categories`,
  `training-centers`, `promotions`) — move each one out when its feature is generated.
- `Infrastructure\WeekRange` snaps a `DateOnly` to its Monday–Sunday week (FeaturedPromoItem
  board). `Infrastructure\SlotOccupiedException` is the unique-key (2627/2601) counterpart of
  `EntityInUseException`; controllers return 409 for both.
- `Controllers\RowAuditsController` (`GET /api/row-audits/{table}/{pk}`) feeds the
  row-audit badge on the frontend.
- Dapper 2.1.79 maps `DateOnly` natively — no type handler in `Program.cs`.
- `Program.cs` ends with `public partial class Program;` so tests can reference it.
- CORS policy `LocalhostCorsPolicy` allows any loopback origin (`uri.IsLoopback`).
- Swagger: **Swashbuckle 7.2.0**, pinned. `Microsoft.AspNetCore.OpenApi` / `AddOpenApi`
  was deliberately removed — don't reintroduce it. Swagger UI is on in all environments.
- No `UseHttpsRedirection`; HTTP-only on port 5000 by design.

## Frontend (`src\CMS.NG\src\app`)

- `core/services/lookup.service.ts` — one method per lookup endpoint.
- `core/components/row-audit-badge` — shown in detail/edit toolbars (`#start` slot).
- `core/components/qr-code` (`app-qr-code`) — `text` / `title` / `fileName` / `size` inputs;
  renders via `core/services/qr-code.service.ts` (wraps the `qrcode` npm package, listed in
  `angular.json` `allowedCommonJsDependencies`). Spy on `QrCodeService.prototype.toCanvas`
  in tests to assert the encoded text; the download composites the title under the symbol
  into a PNG data URL.
- `core/utils/session-storage.util.ts` — guarded read/write for `{entity}-list-*` keys.
- `core/utils/date.util.ts` — `toIso` / `fromIso` / `addYears` / `addDays` / `startOfWeek`
  (Monday) / `formatMonthDayWeekday` (`3/16 (一)`) for `date` columns (local components only,
  never `toISOString`).
- `core/utils/ascii.validator.ts` — `asciiValidator` for `varchar` columns
  (`partner.model.ts` re-exports it for older imports).
- PrimeNG configured in `app.config.ts` via `providePrimeNG` (**Aura** preset) alongside
  `provideAnimationsAsync()` and `provideHttpClient(withFetch())`. `primeicons.css` is
  loaded from `angular.json` `styles`, not SCSS.
- API base URL: `src\environments\environment*.ts` (`apiBaseUrl`). `environment.ts` is
  production; `environment.development.ts` replaces it via `fileReplacements`.
- Path aliases: `@environments/*`, `@app/*`, `@core/*`, `@features/*`.

### App shell

`app.ts` is the shell: topbar + collapsible sidebar (CSS grid) + `router-outlet`. The
sidebar is a PrimeNG `PanelMenu` driven by the `menuItems` array — add features there.

Current menu:
- `首頁 Home` → `上稿作業 FeaturedPromoItem` (`/home/featured-promo-items`)
- `系統管理 Admin` → `角色 AppRole` (`/admin/app-roles`), `使用者 AppUser`
  (`/admin/app-users`), `發布狀態 PublishStatus` (`/admin/publish-statuses`)
- `課程管理 Course` → `合作夥伴 Partner` (`/course/partners`), `課程群組 CourseGroup`
  (`/course/course-groups`), `課程 Course` (`/course/courses`), `認證 Certification`
  (`/course/certifications`)

`<p-toast/>` and `<p-confirmdialog/>` are rendered once in `app.html`; `MessageService`
and `ConfirmationService` are provided app-wide in `app.config.ts`. Feature pages only
inject them.

### Testing rules

- Karma + Jasmine (Angular default) — do not migrate to Vitest/Jest.
- Any TestBed that mounts `App` or a feature page must provide `provideRouter([])`,
  `provideNoopAnimations()`, `MessageService` and `ConfirmationService`, or PrimeNG
  animations will throw.

# Shared feature infrastructure

Read this when building or modifying a CRUD feature (`/crud`), a lookup, or audit logging.
Overview and always-on conventions are in `CLAUDE.md`.

## Repo layout

```
database\        *.sql schema files (source of truth for the data model)
spec\            code-gen.convention.md, feature specs in spec\{sub-system}\{Table}.md
docs\claude\     reference notes for Claude (this folder)
src\
  global.json    pins the SDK to 9.0.317
  CMS.API\       .NET 9 Web API, controllers + Dapper
  CMS.API.Tests\ xUnit + Moq
  CMS.NG\        Angular 20, standalone components, PrimeNG (Aura)
```

## Backend (`src\CMS.API`)

- `Infrastructure\IDbConnectionFactory` — repositories call `CreateOpenConnectionAsync`.
  Dapper only, no EF. Everything async with `CancellationToken` flowed through.
- `Infrastructure\RowAuditWriter.cs` (`IRowAuditWriter`) writes `dbo.RowAudit` on the
  caller's connection/transaction. User name is `User.Identity.Name` — the JWT `userId` claim —
  and falls back to `"system"` when unauthenticated.
- `Infrastructure\AuditHelper.ChangedColumns` diffs a model against a request by property
  name for UPDATE audit descriptions.
- `Infrastructure\EntityInUseException` — repositories throw it on SQL error 547 (FK
  violation); controllers return 409.
- `Infrastructure\PasswordHasher` (SHA-256 → lowercase hex) and
  `Infrastructure\AppConfigJson` (reads `defaultPassword` / `symmetricSecurityKey` out of the
  `SysConfig.appConfig` JSON via `ExtractString`; throws `AppConfigException` → controllers
  return 500).
- **Login** — `Controllers\AuthController` (`POST /api/auth/login`, body `{ userId, password }`)
  with `Repositories\IAuthRepository` (credential row incl. `PasswordHash`, role ids, signing key —
  the only SELECT that reads `PasswordHash`) and `Infrastructure\JwtTokenIssuer` (`IJwtTokenIssuer`,
  HS256, 24 h, issuer `CMS.API`, claims `sub`/`userId`/`userName` + one `role` per `AppUserRole`).
  The signing secret is `SysConfig.appConfig.symmetricSecurityKey`, read per request (≥ 32 UTF-8
  bytes or `AppConfigException`). `TimeProvider` is registered (`TimeProvider.System`) so tests pin
  the clock with `Tests\Infrastructure\FixedTimeProvider` (`Advance(TimeSpan)` for cache tests).
- **Profile** — `PUT /api/auth/profile` (`AuthController.UpdateProfile`, body `UpdateProfileRequest`
  = `{ userName }` only) trims and updates `AppUser.UserName` for the token's `userId` claim via
  `IAuthRepository.UpdateUserNameAsync` (RowAudit `UPDATE` on `AppUser`); blank → 400
  `ValidationProblem`, no row → 404. Returns `ProfileResponse { userId, userName, roles }` (roles echo
  the token). `AuthRepository` now also takes `IRowAuditWriter`.
- **Change password** — `POST /api/auth/change-password` (`AuthController.ChangePassword`, body
  `ChangePasswordRequest` = `{ currentPassword, newPassword, confirmNewPassword }`, no `UserId`): verifies
  the current password against the stored hash (same constant-time compare as Login), applies
  `Infrastructure\PasswordPolicy.IsCompliant` (≥ 8, ≥ 3 of 4 classes; `PasswordPolicy.Message` is the
  Chinese rule text), checks the confirmation, then `IAuthRepository.UpdatePasswordAsync(userId, hash,
  utcNow)` (RowAudit `UPDATE` on `AppUser`). 204 / 400 `ValidationProblem` keyed by field / 404.
  `AuthController` now also takes `TimeProvider` (tests pass `FixedTimeProvider` and assert the timestamp)
  and `IPasswordStampCache` (invalidated after the write). Angular mirror of the rule:
  `core/utils/password.validator.ts`.
- **Token revocation on password change** — `JwtTokenIssuer` writes an explicit `iat`;
  `ConfigureJwtBearerOptions.OnTokenValidated` compares it with `AppUser.PasswordUpdatedTime` from
  `Infrastructure\PasswordStampCache` (`IPasswordStampCache`: `GetAsync(userId)` cached 1 min per user via a
  scoped `IAuthRepository.GetPasswordStampAsync`, `Invalidate(userId)`; static
  `IsIssuedBeforePasswordChange(iat, stamp)` compares whole seconds). Older token → `context.Fail` → 401;
  no row → 401; lookup exception → logged, token kept. Registered as a singleton in `Program.cs`.
  `CmsApiFactory` sets `GetPasswordStampAsync` up with a null stamp by default. Frontend after a 204:
  `AuthService.clear()` + `router.navigate(['/login'], { queryParams: { reason: PASSWORD_CHANGED_REASON } })`;
  `LoginComponent.notice` renders it.
- **Authorization** — `Program.cs` adds a global `AuthorizeFilter` (`RequireAuthenticatedUser`) to MVC;
  `AuthController.Login` is the only `[AllowAnonymous]` **action** in the API (a reflection test enforces
  this — no class-level `[AllowAnonymous]`, or every action on that controller would be public). Bearer
  scheme: `Infrastructure\ConfigureJwtBearerOptions` (issuer `CMS.API`, no audience, 1 min skew,
  `NameClaimType = userId`, `RoleClaimType = ClaimTypes.Role`) validates with the key from
  `Infrastructure\SigningKeyCache` (`ISigningKeyCache`, singleton): `OnMessageReceived` awaits
  `RefreshAsync` (re-reads `SysConfig.appConfig.symmetricSecurityKey` through a scoped `IAuthRepository`
  once per `CacheDuration` = 1 min), the sync `IssuerSigningKeyResolver` returns `CurrentKeys`. A DB error
  keeps the last key; an `AppConfigException` clears it (every protected call → 401, never 500).
  Swagger has a Bearer "Authorize" button. Packages: `Microsoft.AspNetCore.Authentication.JwtBearer` 9.0.19
  (API), `Microsoft.AspNetCore.Mvc.Testing` 9.0.19 (tests). Integration tests host the real pipeline with
  `Tests\Infrastructure\CmsApiFactory` (`WebApplicationFactory<Program>`; `IAuthRepository` and
  `IPublishStatusRepository` replaced by Moq mocks, `IssueToken(...)` mints tokens) — add more repository
  mocks there when a test needs another controller.
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
- **Auth** — `core/services/auth.service.ts` (`AuthService`): `login()` POSTs `/auth/login` and stores
  the `UserProfile` (`core/models/auth.model.ts`) under sessionStorage key `AUTH_PROFILE_KEY`
  (`auth-profile`, **never** localStorage); signals `isAuthenticated` / `userName` / `roles` /
  `isAdmin` (roles decoded from the token by `core/utils/jwt.util.ts`, claim `role` or the long
  ClaimTypes URI — no API call); `accessToken()` reads storage directly; `clear()` wipes the whole
  sessionStorage (list filters too); `logout()` = clear + navigate to `LOGIN_PATH` (`/login`).
  `core/interceptors/auth.interceptor.ts` adds `Authorization: Bearer` and on 401 calls `logout()`
  (except for the login URL). `core/guards/auth.guard.ts` (`canActivateChild` on the guarded group in
  `app.routes.ts`) redirects to `/login?returnUrl=`. `features/auth/login` is the public page (honours
  only in-app `returnUrl`s). `updateProfile(userName)` PUTs `/auth/profile` and patches the stored
  profile's `userName` (token unchanged), which is what `features/auth/profile` (`/profile`, 個人資料
  My Profile: read-only UserId + role tags, editable UserName) and the topbar rely on. Tests:
  `src/app/testing/auth-testing.ts` (`fakeJwt`, `fakeProfile`, `seedSignedInUser(roles)`) — seed
  **before** the TestBed creates `AuthService`.
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

`app.ts` is the shell: topbar + collapsible sidebar (CSS grid) + `router-outlet`. Topbar and
sidebar render only while signed in (`.app-shell--anonymous` gives the login page the full
viewport); the topbar shows `auth.userName()` as a link to `/profile` (`a.app-topbar__user`,
個人資料 My Profile) and a `登出` button. The sidebar is a PrimeNG
`PanelMenu` driven by the module-level `MENU_ITEMS` array — add features there; the `menuItems`
computed drops the `ADMIN_MENU_LABEL` group unless `auth.isAdmin()`. Routes: `/login` is public;
everything else sits under a `''` parent with `canActivateChild: [authGuard]`, whose `''` child
redirects to `/home/featured-promo-items`.

Current menu:
- `首頁 Home` → `上稿作業 FeaturedPromoItem` (`/home/featured-promo-items`)
- `系統管理 Admin` (role `Admin` only) → `角色 AppRole` (`/admin/app-roles`), `使用者 AppUser`
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
  animations will throw. `App` also needs `provideHttpClient()` (it injects `AuthService`) and
  a profile seeded with `seedSignedInUser([...])` before `createComponent`, otherwise the shell
  renders signed-out (no topbar / sidebar). Clear `sessionStorage` in `afterEach`.

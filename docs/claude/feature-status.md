# Feature status

Read this when choosing the next table to scaffold or when touching an existing feature's
non-obvious behaviour. Specs live in `spec\{sub-system}\{Table}.md`; build with `/crud`.

Totals as of 2026-09-10 (v1.0.0.0): **536 xUnit + 457 Karma** tests passing; `ng build` succeeds
(the initial bundle exceeds the 500 kB budget *warning* because of PrimeNG shared chunks —
not an error). Everything through Login, JWT authorization, My Profile, Change Password
(with token revocation) and the forced change for default-password logins is on `develop`.

## Built

Summary: PublishStatus, AppRole, AppUser, Partner, CourseGroup, Course (detail QR code,
list in-place editing), Certification, the custom FeaturedPromoItem weekly board
(`首頁 Home` menu), and Login + JWT authorization + My Profile + Change Password (+ forced
change after a default-password login) end-to-end (spec `spec\auth\Auth.md`). Details per feature follow.

**PublishStatus** (`spec\admin\PublishStatus.md`) — first feature; introduced the
RowAudit plumbing and the lookup endpoint pattern. Commit `9f2fd4c`.

**AppRole** (`spec\admin\AppRole.md`) — string PK `RoleId` (routes use `{id}` with no
`:int`; the service URL-encodes it), `pkid` is display-only. N-N with `AppUser` via
`AppUserRole` managed by a `p-multiselect` (delete-then-reinsert in one transaction).
Introduced `StringLookupItem` and `ILookupRepository`/`LookupRepository`.

**Partner** (`spec\course\Partner.md`) — smallint IDENTITY PK, no FKs. Five inbound
references (Course, Certification, PartnerCourseGroup, Seminar, Promotion2) shown as link
buttons. `AppKey` / `ImageFilename` are `varchar`, so both sides enforce printable-ASCII.
`PartnerCourseGroup` is a child entity, not an N-N junction (it has its own columns).
First entry in the `課程管理 Course` menu group.

**CourseGroup** (`spec\course\CourseGroup.md`) — smallint IDENTITY PK, one `Description`
column, referenced by `Course` and `PartnerCourseGroup`. Default sort `pkid DESC` (no
DisplayOrder column).

**Course** (`spec\course\Course.md`) — int IDENTITY PK. FKs to Partner / CourseGroup
(nullable) / PublishStatus are **JOINed** so every row carries `partnerName`,
`courseGroupDescription`, `publishStatusDescription`. N-N `CourseInCertification` +
`CourseJobCategories` via two `p-multiselect`s (both junctions `ON DELETE CASCADE`).
`date` columns as `DateOnly` with `p-datepicker`; `ScheduleOff` auto-defaults to
`ScheduleOn + 10y`. The detail page's `基本資料` card shows a **QR code** (`app-qr-code`,
`core/components/qr-code`) encoding `https://www.uuu.com.tw/Course/Show/{pkid}/{courseId}`
(`courseShowUrl` in `course.model.ts`), captioned with `courseId`, downloadable as
`{courseId}.png`. Print-to-PDF is the separate **print view** (see below). Deliberately **not**
built from the sample spec: `/copy`, CourseRelatedLink / CourseRecomm sub-panels; `ClassSection` is not in the schema.
The **list page edits cells in place**: double-click (never single-click) opens a PrimeNG
editor matching the column, blur / Enter / option-select commits, Escape cancels; `主代碼`,
`原廠`, `課程群組` stay read-only. State, validation (same rules as the form, plus
`scheduleOn ≤ scheduleOff` against the row) and draft→model conversion live in
`course-list/course-inline-edit.ts`; the component owns the editing state itself rather than
using `pEditableColumn`, whose host listener is hard-wired to single click. A commit is
optimistic: the row is patched, then `getById` (for the N-N lists the list rows lack) →
`update` with every editable column taken from the list row; on error the cell reverts and a
toast is shown. Inline errors keep the cell open and block opening another cell.

**Certification** (`spec\course\Certification.md`) — int IDENTITY PK, required `Partner_pkid`
(JOINed as `partnerName`), nullable **`nchar(100)` Title** (`RTRIM` in every SELECT, blank → NULL
on write, shown as `(無名稱)` when null). N-N `CourseInCertification` + `CertificationJobCategories`
via two `p-multiselect`s. **Delete removes both junction sets in the same transaction** instead of
returning 409 (they are payload-free links this feature owns; `FK_CourseInCertification_Certification`
does not cascade). Query filters include `CoursePkid` / `JobCategoryPkid` via `EXISTS` on the
junctions. No child entities, so no link buttons; the list accepts `partnerPkid` from the Partner
pages. The `certifications` lookup moved here from `LookupRepository`.

**FeaturedPromoItem** (`spec\custom\FeaturedPromoItem\FeaturedPromoItem.spec.md` + three PNG
mockups) — **custom, not `/crud`-shaped.** A weekly home-page board under the `首頁 Home` menu:
one `p-tabs` tab per TrainingCenter, a Monday–Sunday navigator, three slots per day edited
**inline** (`FeaturedPromoFormComponent` is a child of the list, no detail/form routes).
`GET /api/featured-promo-items/week?trainingCenterPkid=&date=` snaps any date to the week via
`Infrastructure\WeekRange`. `POST /{id}/move-up|move-down` swaps slots in one transaction, parking
the moving row on slot 0 so the unique `(ScheduleOn, TrainingCenter_pkid, Slot)` index never
trips; a unique violation on create/update (2627/2601) → `SlotOccupiedException` → 409. PromoCode
is a `p-autocomplete` over `GET /api/lookups/promotions?keyword=` (`PromotionLookupItem` carries
Topic/Description, which pre-fill blank fields). 複製/貼上 is an in-memory clipboard signal on the
list. Session key `featured-promo-list-filters` stores `{ trainingCenterPkid, weekStart }`.

**AppUser** (`spec\auth\AppUser.md`) — string PK `UserId`, N-N with `AppRole` via
`AppUserRole`. **`PasswordHash` never crosses the API**: excluded from request and Angular
models; create seeds it with `PasswordHasher.Hash` of `SysConfig.appConfig.defaultPassword`; update
never touches it; `POST /api/app-users/{id}/reset-password` re-applies the default
(detail page has a 重設密碼 button), then calls `IPasswordStampCache.Invalidate(userId)` so the target's
existing token is rejected on its very next request rather than up to a minute later. `PasswordUpdatedTime`
on create and on reset is written from the injected `TimeProvider`, not SQL `GETUTCDATE()`: it is compared
against the token's `iat`, which comes from the same clock, and a database clock a second behind the web
server's made the reset revoke nothing. The reset's RowAudit description is plain `PasswordHash` — it used to
read `PasswordHash (reset to default)`, which turned the readable 異動紀錄 trail into a list of accounts
currently standing on the shared default password. The `app-users` lookup lives in `AppUserRepository`.
**Administrators only** since the security fix below: the whole controller and the `app-roles` lookup carry
`[Authorize(Policy = AuthorizationPolicies.Admin)]`, and the SPA routes carry `adminGuard`.

**Login** (`POST /api/auth/login`; spec `spec\auth\Auth.md`, derived from the implementation) — body `{ userId, password }`;
`AuthController` loads the `AppUser` row via `IAuthRepository.GetCredentialAsync`, then requires an
**ordinal** `UserId` match (the DB lookup may be collation-insensitive), `IsActive = 1`, and a
constant-time `PasswordHasher.Verify` of the password against `PasswordHash`. Any failure →
**401** with the single generic body `{ "message": "帳號或密碼錯誤。" }` (`AuthController.InvalidCredentialsMessage`);
roles and signing key are never queried on failure. Success → `{ userId, userName, accessToken }`
(`LoginResponse`, no password member). The JWT (`JwtTokenIssuer`) is HS256 signed with
`SysConfig.appConfig.symmetricSecurityKey` read at request time, expires 24 h after issue, and carries
`sub`/`userId`/`userName` plus one `role` claim per `AppUserRole.RoleId`. Missing/short key → 500
Problem (`系統設定錯誤`). Package: `System.IdentityModel.Tokens.Jwt` 8.22.0.

**JWT authorization** (spec `spec\auth\Auth.md`) — every controller except `AuthController` requires a valid Bearer
token (global `AuthorizeFilter`; missing/invalid/expired token or a token signed with another key → **401**
with `WWW-Authenticate: Bearer`). Validation uses the same `SysConfig` key via `SigningKeyCache` (1-minute
cache; DB failure keeps the last key, bad `appConfig` → 401 for everyone, never 500). RowAudit records
the `userName` claim as `UserName` (fallback `userId`; see the generic writer below). Frontend: `/login` page → profile in **sessionStorage** (`auth-profile`);
`authInterceptor` adds the Bearer header and turns any 401 (except from login) into clear-session +
`/login`; `authGuard` protects every other route (`/login?returnUrl=…`); the topbar shows the UserName and
a `登出` button (clears sessionStorage, including remembered list filters); the `系統管理 Admin` menu group is
only rendered when the token's roles include `Admin`. Tests: `JwtBearerAuthorizationTests` (in-memory host
via `CmsApiFactory`), `SigningKeyCacheTests`; Karma specs for `AuthService`, interceptor, guard, JWT util,
`LoginComponent` and the shell. Role-based authorization now exists for the account / role endpoints only
(see the security hardening entry below); everything else still checks authentication alone. Not built:
per-endpoint or `PermissionLevel`-driven permissions, token refresh / expiry warning.

**My Profile** (`/profile`, 個人資料; spec `spec\auth\Auth.md`) — `PUT /api/auth/profile` `{ userName }` updates the
caller's own `AppUser.UserName`; the user comes **only** from the token's `userId` claim (the request type has
no `UserId` / `RoleIds` member, so such JSON is ignored — an integration test posts them and checks the
repository was called with the token user). UserName is trimmed; blank → 400, unknown user → 404. Login is
now the only anonymous *action* (`[AllowAnonymous]` moved from the class to `Login`). The page shows UserId
read-only, roles as read-only `p-tag`s from the token, and an editable UserName; save refreshes session
storage and the topbar via `AuthService.updateProfile`. The topbar user name links to the page. The token's
`userName` claim is not re-issued after a rename (nothing reads it server-side).

**Change Password** (second card on `/profile`, 變更密碼; spec `spec\auth\Auth.md`) — `POST /api/auth/change-password`
`{ currentPassword, newPassword, confirmNewPassword }` → **204**. `AuthController.ChangePassword` takes the
user from the token, then in order: current password must hash to the stored `PasswordHash` (else 400
`CurrentPassword` = `目前密碼錯誤。`, nothing written), new password must pass `Infrastructure\PasswordPolicy`
(≥ 8 chars and ≥ 3 of upper / lower / digit / ASCII symbol; else 400 `NewPassword` = `PasswordPolicy.Message`),
confirmation must equal it exactly (else 400 `ConfirmNewPassword`). Then
`IAuthRepository.UpdatePasswordAsync(userId, PasswordHasher.Hash(new), TimeProvider.GetUtcNow())` sets `PasswordHash` +
`PasswordUpdatedTime` and writes RowAudit `PasswordHash (changed by user)`; no row → 404. No hash crosses the
API in either direction. Frontend: `AuthService.changePassword`, `core/utils/password.validator.ts`
(`meetsPasswordPolicy` mirrors the API rule, `passwordPolicyValidator`, `passwordsMatchValidator`) and a
bilingual policy message under the field; API field errors are pinned under the matching input.

**A password change ends every session** — tokens carry `iat`; the bearer handler's `OnTokenValidated`
(`ConfigureJwtBearerOptions`) loads the user's `PasswordUpdatedTime` through `PasswordStampCache` (singleton,
per-user, 1-minute TTL, `IAuthRepository.GetPasswordStampAsync`) and fails the token when `iat` < that time
at whole-second resolution (a login in the same second as the change is still accepted); a missing user row
also fails; a DB error during the lookup is logged and the token kept. `ChangePassword` calls
`IPasswordStampCache.Invalidate(userId)`, so the token that made the change is rejected on the very next
request. Frontend: on 204 the profile page calls `AuthService.clear()` and navigates to
`/login?reason=password-changed`; `LoginComponent` shows a bilingual info notice for that reason. Tests:
`AuthControllerChangePasswordTests` (38 cases), `PasswordPolicyTests`, `PasswordStampCacheTests`,
`JwtBearerAuthorizationTests` (token before/after the change, unknown user, DB failure, and an end-to-end
change → old token 401 → old password 401 → new password logs in); Karma specs for the validator, the service
call, the form (empty / weak / mismatch never call the API; success clears the session and redirects) and the
login notice.

**Forced password change after a default-password login** (spec `spec\auth\Auth.md`) — there is no DB flag:
`AuthController.Login` compares the accepted password with `SysConfig.appConfig.defaultPassword`
(`IAuthRepository.GetDefaultPasswordAsync`, ordinal) and, when equal, issues the token with claim
`mustChangePassword = true` (`JwtTokenIssuer.MustChangePasswordClaim`) and returns `LoginResponse.MustChangePassword`.
The global `Infrastructure\PasswordChangeRequiredFilter` (registered after the `AuthorizeFilter`, so a bad token is
still 401) answers **403** `{ message: "請先變更密碼後再使用系統。" }` to every action except the one carrying
`[AllowPasswordChangeRequired]` — only `AuthController.ChangePassword` (reflection test). Changing the password
revokes the flagged token as usual; the next login (new password) is clean. Both admin paths (create, reset) are
covered because detection happens at login; tokens issued before this feature carry no claim and are unaffected.
Frontend: `AuthService.mustChangePassword` (claim read from the stored token by `mustChangePasswordFromToken`;
the flag is **not** stored in session storage), `authGuard` redirects every URL except `/change-password` there,
`LoginComponent` goes straight to `/change-password` (ignoring `returnUrl`), the interceptor treats a 403 for a
flagged session as "go to `/change-password`" (no logout), and the shell is locked (`app-shell--locked`: no
sidebar, no menu toggle, the topbar name is plain text with 請先變更密碼; 登出 stays). The 變更密碼 form is now
the shared `features/auth/change-password-form` (used by `/profile` and by the new `features/auth/change-password`
page, which shows a bilingual warning only when the session is flagged). Tests: `AuthControllerTests` (flag /
no flag / ordinal / 500 / never read on failure), `JwtTokenIssuerTests`, `PasswordChangeRequiredFilterTests`,
`JwtBearerAuthorizationTests` (403 on protected + profile, change-password reachable, end-to-end default login →
change → old token 401 → new login unflagged, expired flagged token is 401, filter order, only-allowed-action);
Karma specs for the JWT helper, `AuthService`, guard, interceptor, login redirect, the shared form (moved from
`profile.component.spec`), the new page and the locked shell.

**Generic RowAudit writer** (branch `feature-row-audit`, 2026-09-08) — `IRowAuditWriter` gained reflection-based
`LogInsertAsync` / `LogUpdateAsync` / `LogDeleteAsync` (pkid or `[AuditKey]` → `PrimaryKeyValues`, first string
property or changed-property list → `ActionDesc`, no row for a no-change update, `ActionDesc` cut at 1000).
`UserName` now comes from the `userName` claim (was `Identity.Name` = `userId`) and `DateTime` from `TimeProvider`
instead of `GETUTCDATE()`. **Wired into all eight CRUD repositories** (AppRole, AppUser, Certification, Course,
CourseGroup, FeaturedPromoItem, Partner, PublishStatus): load → change → reload → `Log*` on the change's own
transaction. Visible differences from before: INSERT/DELETE descriptions are the first string column (Course:
`Title`, was `CourseId`; Certification: `Title` or NULL, was `(無名稱)`; FeaturedPromoItem: `Topic`, was
`date TC slot text`); a save that changes nothing writes **no** row (was `(no changes)`); AppRole / AppUser keep
`RoleId` / `UserId` as `PrimaryKeyValues` through `[AuditKey]`, and JOINed labels / counts are `[AuditIgnore]`d.
Custom `WriteAsync` descriptions remain for password reset, profile / password change and the slot swap. The same
branch fixed a latent crash: Dapper 2.1.79 cannot bind or read `DateOnly` at all (`Infrastructure\DapperTypeHandlers`).
Tests: `RowAuditWriterTests` (20), `AuditHelperTests` (+2), `Repositories\PartnerRepositoryTests` (13) and
`CourseRepositoryTests` (4) on `RecordingDbConnection` + `ListDataReader`.

**異動紀錄 History badge** (branch `feature-row-audit`, 2026-09-08) — `GET /api/row-audits?tableName=&pkid=`
(`RowAuditsController.GetForRecord`, replaces the old `/api/row-audits/{table}/{pk}?take=`) returns the record's
**full** trail newest first as `{ dateTime, userName, actionType, actionDesc }`; a missing filter → 400
`ValidationProblem`. **`tableName` `AppUser` or `AppRole` requires the `Admin` role** (`AdminOnlyTables`,
`StringComparer.OrdinalIgnoreCase`, matched after `Trim()`) → **403** `只有管理者可以查看帳號與角色的異動紀錄。`;
every content table's trail stays open to any signed-in user. The gate lives in the action rather than in an
attribute because the table is a *query-string value*: the Admin policy on `AppUsersController` /
`AppRolesController` guarded the rows but not their history, so any signed-in user could read who changed
which account and when. `core/components/row-audit-badge` (`<app-row-audit-badge tableName [pkid]>`) is now a
`p-button` labelled 異動紀錄 History that shows the newest entry inline (`Update by alice · 2026-06-04 14:30`,
`尚無異動紀錄 No history`, `無法載入 Unavailable`) and opens a `p-dialog` + `p-table` with the whole trail
(`尚無異動紀錄 No history yet` when empty); it is on all 14 detail / edit toolbars. The input was renamed `pk` →
`pkid`; the service method `getForRow(table, pk, take)` → `getForRecord(table, pkid)`. Tests:
`RowAuditsControllerTests` (19, including the admin-table gate and its casing / whitespace variants),
`RowAuditRepositoryTests` (6), `RowAuditsEndpointTests` (14, in-memory host: 403 for an editor, 200 for an
admin, content tables still open) and
`row-audit-badge.component.spec.ts` (12: inline latest, empty state, dialog trail, dash for null desc, failure
state, string keys, no pkid, record change).

**Course print view 列印PDF** (branch `feature-course-pdf`, 2026-09-09; design `docs\designs\course-pdf-export.md`,
spec `spec\course\Course.md` *Print view*) — a customer-facing one-pager, not the detail page printed. The detail
toolbar's 列印PDF button opens `/course/courses/{pkid}/print` in a new tab (`window.open`, **no `noopener`** so the
sessionStorage session carries over). The route carries `data: { chromeless: true }`: `app.ts` derives a `chromeless`
signal from `NavigationEnd` + the leaf route's data and `app.html` then renders nothing around its single
`<router-outlet>` (no shell grid, topbar, sidebar, toast or confirm dialog), which is what makes multi-page printing work.
`features/courses/course-print` (`ViewEncapsulation.None`, root class `.course-print`) loads the course, then its
`PublishStatus` (failure → treated as unpublished), shows the customer field set only (internal fields, 認證, 職務類別,
相關資料 and the audit badge are absent), omits null / whitespace-only text blocks, renders the QR (`showDownload=false`)
only for published courses, sets `document.title` to `{courseId} {title}` (default PDF name), writes
`--print-course-id` / `--print-date` on `<html>` as quoted CSS strings for the `@page` margin-box footer
(簡介代碼 / 列印日期 / 第 n 頁, Chrome/Edge 131+; cleared on destroy) and calls `window.print()` **once** inside
`afterNextRender` after the course and the QR have settled. `QrCodeComponent` gained `showDownload` (input) and
`settled` (`'ready' | 'error'` output). No API change, no new packages. Tests: `course-print.component.spec.ts` (17),
`course-detail.component.spec.ts` (+2), `qr-code.component.spec.ts` (+2), `app.spec.ts` (+2 chromeless).

**Security hardening from the /cso audit** (branch `feature-course-pdf`, 2026-09-10; report
`.gstack\security-reports\2026-09-10-022718.json`) — three findings fixed.

1. **Admin authorization** (was CRITICAL: the API checked authentication only, so any signed-in user could
   create accounts, delete them, rewrite role membership via `AppRoleRequest.UserIds`, or reset an
   administrator's password to the shared default and take the account over). `Program.cs` now registers
   `AuthorizationPolicies.Admin` = `RequireRole("Admin")`; `AppUsersController`, `AppRolesController` and
   `LookupsController.AppUsers` / `.AppRoles` carry `[Authorize(Policy = …)]`. A non-administrator gets **403**
   and the repository is never reached; a missing token is still **401** (the global `AuthorizeFilter` runs
   first). The SPA mirrors it with `core/guards/admin.guard.ts` on the two `admin/app-*` route groups,
   redirecting to `/home/featured-promo-items`. Content CRUD is unchanged — still any authenticated user.
2. **Password storage** (was HIGH: unsalted single-round SHA-256). `PasswordHasher` now has `Hash` /
   `Verify` / `IsLegacyFormat` on top of PBKDF2-HMAC-SHA256, 210 000 iterations, 16-byte random salt, stored
   as `pbkdf2-sha256$<iterations>$<salt>$<hash>` (90 chars, fits `nvarchar(800)`). Legacy hex rows still
   verify and are rewritten by `IAuthRepository.UpgradePasswordHashAsync` on the owner's next login —
   `PasswordUpdatedTime` untouched (it is the revocation stamp) and no RowAudit row (the credential did not
   change). A failure there is logged and swallowed so the login still succeeds. Verified end-to-end against
   the local `CMS` database: the seeded `admin@example.com` row migrated from 64-char hex to PBKDF2 on login,
   the timestamp stayed at `2026-01-01T00:00:00`, and no audit row appeared.
3. **Swagger** (was MEDIUM: served unauthenticated in every environment, because the global `AuthorizeFilter`
   is an MVC filter and never covers middleware). `app.UseSwagger()` / `UseSwaggerUI()` are now inside
   `if (app.Environment.IsDevelopment())`. Confirmed by probe: 404 under `Production`, 200 under `Development`.

Tests: `Infrastructure\AdminAuthorizationTests` (17: 403 per admin endpoint with the repository never called,
403 for a no-roles token, 200 for an admin, content still open to an editor, 401 beats 403, a flagged
must-change-password admin still 403, plus reflection tests pinning the guarded controller and lookup sets),
`PasswordHasherTests` (rewritten, 24), `AuthControllerTests` (+6 legacy-migration cases),
`AuthControllerChangePasswordTests` (salted-hash assertions), `admin.guard.spec.ts` (6).
Also fixed a pre-existing Karma flake: `auth.interceptor.spec.ts` verified an HTTP backend in `afterEach`
that its two pure `serverErrorDetail` specs never created, so the suite failed whenever Jasmine's random
order ran them first.

**Security headers** (2026-09-10) — `Infrastructure\SecurityHeadersMiddleware` is registered **first** in
`Program.cs` and hooks `Response.OnStarting`, so every response the API produces carries `nosniff`,
`X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Cache-Control: no-store` and
`Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` — 200s, the bearer 401, the admin-policy
403 and the exception middleware's 500 alike. `/swagger` requests get a looser CSP (inline script/style) so
Swashbuckle's UI still runs, and are not `no-store`. HSTS is emitted only on HTTPS requests. Headers are set
only when absent, so `GlobalExceptionMiddleware`'s own `Cache-Control` survives. Kestrel's `Server` header is
off. The SPA's equivalent set lives in `deploy\CMS.NG\web.config.template` (adds `Permissions-Policy`, removes
IIS's `Server` / `X-Powered-By`, HSTS as an outbound rule conditional on `{HTTPS}`). Its
`script-src 'self'` forced one build change: `angular.json` now sets
`optimization.styles.inlineCritical: false`, because the critical-CSS inliner emits an inline `onload=` the
policy blocks — leave it off. Tests: `Infrastructure\SecurityHeadersTests` (12). Verified in a real Chrome
against the production bundle behind a stand-in for the IIS site: login, course list, detail QR canvas, the
chromeless print view, the admin page and the Swagger UI all render with **zero** CSP violations.

**IIS deployment kit** (`deploy\` + `DEPLOY-IIS.md`, 2026-09-10; copied from
`C:\course\ai-full-stack\demo\iis-deploy` by its `copy-to-project.ps1`) — `setup-iis.ps1` (once, elevated)
and `deploy.ps1` (every time) publish the API and build the SPA, stamp both `web.config`s from templates,
cycle `CMS.API.Pool`, and copy to `C:\VHome\CMS\{API,NG}`; sites `CMS` (:80) and `CMS.API` (:5001), with
`/api/*` reverse-proxied by URL Rewrite + ARR so the SPA is same-origin. The one source change it required:
production `environment.ts` now has `apiBaseUrl: '/api'` (was the dev placeholder `http://localhost:5000/api`,
which would have sent the deployed SPA past the proxy to a port nothing listens on); `environment.development.ts`
is unchanged, so `ng serve` and every Karma spec (they build URLs from `environment.apiBaseUrl` symbolically)
are unaffected. The kit's original stamps `ASPNETCORE_ENVIRONMENT=Development` to keep Swagger as a demo
prop; the repo copy stamps **`Production`**, so a deployed API never serves `/swagger` (Development-only by
design) and both scripts print `GET /api/publish-statuses` (401 = up) as the smoke test instead. Not yet
exercised end-to-end on this machine — IIS is not installed here (see `environment.md`). Details:
`feature-infrastructure.md` (layout) and `DEPLOY-IIS.md` (runbook).

**v1.0.0.0 release hardening** (2026-09-10, commits `a7e567a` / `f1cfc41` / `03488f4`) — the last pass before
the first landing on `main`. Four API changes, all described in their own entries above except these:

1. **Concurrent create returns 409, not 500.** AppRole, AppUser and PublishStatus check `ExistsAsync` before
   inserting, but that is a read followed by a write, so two callers racing on the same key both pass it and
   the loser hit the primary key. The three repositories now catch `SqlException` 2627 / 2601 through the new
   `Infrastructure\SqlErrorNumbers` and throw the new `Infrastructure\DuplicateKeyException`; the controllers
   turn it into the same `409` (and the same Chinese message) the pre-check produces. `SqlErrorNumbers` also
   replaces the private `SqlForeignKeyViolation` / unique-violation constants that were re-declared in each of
   the eight repositories — that duplication is how one of them ended up catching a different set.
2. **`AppRoleRepository.Normalize` null-guards its id list** (`IEnumerable<string>?`, `ids ?? []`), matching
   the AppUser / Certification / Course copies. `System.Text.Json` does not honour non-nullable reference
   annotations, so a body carrying `"userIds": null` set the property to null and threw a
   `NullReferenceException` → 500 on input the sibling paths already handled.
3. **Product name.** The shell topbar and `index.html` `<title>` read `CMS`; `CMS-React` was a leftover from
   the scaffold this project was generated from.
4. **Accessibility minimums** (`f1cfc41`, following the `/design-review` findings already on `develop`): a
   global `--cms-error-color` token, a 44 px floor for row-action icon buttons, the previously undefined
   `.muted-if-empty` class, `100dvh` on the shell, and phone wrapping for the promo board's `.week-nav`
   header — see the *Global styles* notes in `feature-infrastructure.md`.

## Lookup endpoints (`/api/lookups/*`)

`app-roles`, `app-users`, `partners`, `course-groups`, `courses`, `certifications`,
`publish-statuses`, `job-categories`, `training-centers`, `promotions?keyword=` (returns
`PromotionLookupItem`, capped at `LookupsController.PromotionLookupLimit`). The last three come
from `LookupRepository` until their features exist.

## Not yet built

Everything else in `course.sql` / `promotion.sql`: JobCategory, CourseFAQ,
CourseRelatedLink, HotCourse, CourseRecomm, LinkDefinition, PartnerCourseGroup,
TrainingCenter, Seminar, Promotion2.

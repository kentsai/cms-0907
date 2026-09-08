# Feature status

Read this when choosing the next table to scaffold or when touching an existing feature's
non-obvious behaviour. Specs live in `spec\{sub-system}\{Table}.md`; build with `/crud`.

Totals as of 2026-09-08: **348 xUnit + 372 Karma** tests passing; `ng build` succeeds
(the initial bundle exceeds the 500 kB budget *warning* because of PrimeNG shared chunks —
not an error). Everything through Login, JWT authorization, My Profile and Change Password
(with token revocation) is committed on `develop`.

## Built

Summary: PublishStatus, AppRole, AppUser, Partner, CourseGroup, Course (detail QR code,
list in-place editing), Certification, the custom FeaturedPromoItem weekly board
(`首頁 Home` menu), and Login + JWT authorization + My Profile + Change Password end-to-end
(spec `spec\auth\Auth.md`). Details per feature follow.

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
`{courseId}.png`. Deliberately **not** built from the sample spec: `/copy`, print-to-PDF,
CourseRelatedLink / CourseRecomm sub-panels; `ClassSection` is not in the schema.
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
models; create seeds it with SHA-256 of `SysConfig.appConfig.defaultPassword`; update
never touches it; `POST /api/app-users/{id}/reset-password` re-applies the default
(detail page has a 重設密碼 button). The `app-users` lookup lives in `AppUserRepository`.
The lowercase-hex hash format is an assumption — no legacy hashes were available.

**Login** (`POST /api/auth/login`; spec `spec\auth\Auth.md`, derived from the implementation) — body `{ userId, password }`;
`AuthController` loads the `AppUser` row via `IAuthRepository.GetCredentialAsync`, then requires an
**ordinal** `UserId` match (the DB lookup may be collation-insensitive), `IsActive = 1`, and a
constant-time, hex-case-insensitive match of SHA-256(password) against `PasswordHash`. Any failure →
**401** with the single generic body `{ "message": "帳號或密碼錯誤。" }` (`AuthController.InvalidCredentialsMessage`);
roles and signing key are never queried on failure. Success → `{ userId, userName, accessToken }`
(`LoginResponse`, no password member). The JWT (`JwtTokenIssuer`) is HS256 signed with
`SysConfig.appConfig.symmetricSecurityKey` read at request time, expires 24 h after issue, and carries
`sub`/`userId`/`userName` plus one `role` claim per `AppUserRole.RoleId`. Missing/short key → 500
Problem (`系統設定錯誤`). Package: `System.IdentityModel.Tokens.Jwt` 8.22.0.

**JWT authorization** (spec `spec\auth\Auth.md`) — every controller except `AuthController` requires a valid Bearer
token (global `AuthorizeFilter`; missing/invalid/expired token or a token signed with another key → **401**
with `WWW-Authenticate: Bearer`). Validation uses the same `SysConfig` key via `SigningKeyCache` (1-minute
cache; DB failure keeps the last key, bad `appConfig` → 401 for everyone, never 500). RowAudit now records
the `userId` claim as `UserName`. Frontend: `/login` page → profile in **sessionStorage** (`auth-profile`);
`authInterceptor` adds the Bearer header and turns any 401 (except from login) into clear-session +
`/login`; `authGuard` protects every other route (`/login?returnUrl=…`); the topbar shows the UserName and
a `登出` button (clears sessionStorage, including remembered list filters); the `系統管理 Admin` menu group is
only rendered when the token's roles include `Admin`. Tests: `JwtBearerAuthorizationTests` (in-memory host
via `CmsApiFactory`), `SigningKeyCacheTests`; Karma specs for `AuthService`, interceptor, guard, JWT util,
`LoginComponent` and the shell. Not built: role-based authorization on individual API endpoints (Admin-only
menu is a UI convenience; the API only checks authentication), token refresh / expiry warning.

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
`IAuthRepository.UpdatePasswordAsync(userId, SHA-256(new), TimeProvider.GetUtcNow())` sets `PasswordHash` +
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

## Lookup endpoints (`/api/lookups/*`)

`app-roles`, `app-users`, `partners`, `course-groups`, `courses`, `certifications`,
`publish-statuses`, `job-categories`, `training-centers`, `promotions?keyword=` (returns
`PromotionLookupItem`, capped at `LookupsController.PromotionLookupLimit`). The last three come
from `LookupRepository` until their features exist.

## Not yet built

Everything else in `course.sql` / `promotion.sql`: JobCategory, CourseFAQ,
CourseRelatedLink, HotCourse, CourseRecomm, LinkDefinition, PartnerCourseGroup,
TrainingCenter, Seminar, Promotion2.

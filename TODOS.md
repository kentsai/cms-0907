# TODOS

Open work, grouped by area and ranked P0 (do next) to P4 (someday). Most of this came out of the
`/ship` review on 2026-09-10 (v1.0.0.0), which ran nine reviewers over the whole codebase; each item
names the file and, where useful, what was measured. Completed items move to the bottom.

## Deploy / infrastructure

### The API app pool owns the database
**Priority:** P0
`deploy\setup-iis.ps1:280` runs `ALTER ROLE db_owner ADD MEMBER [$poolLogin]`. `SysConfig.appConfig`
holds both the JWT signing key and the plaintext default password, so any app-tier compromise reads
them and can mint admin tokens indefinitely. `db_datareader` + `db_datawriter` + `EXECUTE` covers
everything this code does.

### The API is reachable from the network, so the proxy is not a boundary
**Priority:** P0
`deploy\setup-iis.ps1:228` calls `New-Website` with no `-IPAddress`, so the API binds `*:5001` rather
than `127.0.0.1:5001`. `/api/auth/login` can be hit directly, bypassing the SPA site's CSP and security
headers. Bind to loopback.

### No HTTPS anywhere
**Priority:** P0
There is no HTTPS binding, no `UseHttpsRedirection` and no HSTS in effect — `SecurityHeadersMiddleware`
only emits HSTS when `IsHttps`, so it never fires, and the rewrite rule in
`deploy\CMS.NG\web.config.template:42-48` is documented as inert. Bearer tokens and passwords cross the
wire in cleartext.

### Login has no rate limit
**Priority:** P1
`AuthController.cs:47`. No throttle, backoff or lockout, and `PasswordHasher` runs 210,000 PBKDF2
iterations per attempt on an anonymous endpoint — so this is both a password-spraying route and a CPU
amplifier. Add `AddRateLimiter` partitioned by IP and submitted user id.

### Deploy can ship a half-copied build, and the SPA has no web.config during the copy
**Priority:** P1
`deploy\deploy.ps1:215` uses `-ErrorAction SilentlyContinue` on the clear step, so a locked DLL is
swallowed and the script reports success over a partially cleared folder. The Angular copy at `:272`
runs with no pool stop and no atomic swap, so the live site briefly has no rewrite rules, no proxy and
no security headers. Drop `SilentlyContinue`; stage and swap.

### Committed appsettings.json masks a broken deploy
**Priority:** P2
`src\CMS.API\appsettings.json:8-10` ships a real SQLEXPRESS connection string, so a lost
`<environmentVariables>` block falls back to a local unencrypted database instead of failing fast.
`TrustServerCertificate=True` (also `deploy\deploy.ps1:71`) encrypts without validating the certificate,
which matters as soon as SQL is not on this box. `"AllowedHosts": "*"` lets `CreatedAtAction` reflect an
attacker-supplied Host.

### stdout logging is on in production
**Priority:** P2
`deploy\CMS.API\web.config.template:20-21`. ANCM does not roll these files and the global exception
handler writes full stack traces into them — unbounded disk growth.

## Security / authorization

### Deactivating a user, or removing their Admin role, does not end their session
**Priority:** P1
Roles are baked into a 24-hour token and `IsActive` is read only at login
(`ConfigureJwtBearerOptions.cs:72`, `AuthRepository.cs:110` selects only `PasswordUpdatedTime`). A
de-privileged administrator stays an administrator, at the API and not just in the UI, until the token
expires. Extend the stamp to carry `IsActive` and the current roles.

### Login leaks which accounts exist, by timing
**Priority:** P2
`AuthController.cs:47` short-circuits on `user is null`, so an unknown id returns after one indexed
SELECT while a known id pays 210,000 PBKDF2 iterations. Verify against a fixed dummy hash on the null
branch.

### Audit attribution is forgeable and truncates
**Priority:** P2
`RowAuditWriter.ResolveUserName()` records the `userName` claim, which any user can set to `admin` via
`PUT /api/auth/profile`. `RowAudit.UserName` and `PrimaryKeyValues` are `nvarchar(100)` against source
columns of `nvarchar(200)`, so two accounts sharing a 100-character prefix produce indistinguishable
histories. Audit on the immutable `userId` and widen the columns.

### Delete-confirmation dialogs render stored text as HTML
**Priority:** P3
PrimeNG's confirm dialog binds `[innerHTML]`; 16 call sites interpolate API data into it (e.g.
`partner-list.component.ts:133`). Angular's sanitizer runs, so this is not script execution, but a
non-admin editor can still plant a link or block markup inside an administrator's confirmation modal.
Use `pTemplate="message"` with `{{ }}`.

## Performance

### RowAudit has no index for the query every page runs
**Priority:** P0
`database\admin.sql:77` creates it with only the clustered PK on `pkid`, while
`RowAuditRepository.cs:17` filters `TableName + PrimaryKeyValues` and sorts by `[DateTime] DESC`. The
history badge is on 14 templates and the table is append-only across all eight entities, so every detail
page load is a full scan plus a sort that gets slower forever. Add:
`CREATE NONCLUSTERED INDEX IX_RowAudit_Table_Key_DateTime ON dbo.RowAudit (TableName, PrimaryKeyValues, [DateTime] DESC, pkid DESC) INCLUDE (UserName, ActionType, ActionDesc);`
Apply it to existing databases too, not just `admin.sql`.

### No server-side paging on any list endpoint
**Priority:** P1
`CourseRepository.QueryAsync` and its siblings have no `OFFSET`/`FETCH` and no total count;
`course-list.component.ts:160` downloads every row and pages client-side. Add `First`/`Rows` to the
query, return `COUNT(*) OVER()`, and switch the tables to `[lazy]="true"`.

### List queries drag every course's long-form text across the wire
**Priority:** P1
`CourseRepository.cs:19` shares one `SelectColumns` between the detail read and both list reads, so
`Outline` and `TowardCertOrExam` (`nvarchar(MAX)`) are fetched for every row of a grid that renders
neither — up to ~20 KB per row. Split out a narrow `SelectListColumns` and a slim list DTO.

### ~59 kB of unused PrimeNG theme in the initial bundle
**Priority:** P1
`app.config.ts:7` imports the whole `@primeuix/themes/aura` barrel, pulling tokens for ~100 components
into the eager bundle. Measured with the repo's own esbuild: the barrel minifies to 113.5 kB; the ~26
sub-presets actually used come to 54.8 kB. Routes are already fully lazy, so this is the single largest
removable item and the main reason `ng build` exceeds its 500 kB budget (currently 773.61 kB).

### No component uses OnPush
**Priority:** P2
The shell and all 28 feature components run default change detection while state is already
signal-based. `course-list.component.html` binds `editorFor(item, field)` 22 times per row — 1,100 calls
per pass at 50 rows. Move `cellEdit`/`filters` to signals, add `ChangeDetectionStrategy.OnPush`, then
consider `provideZonelessChangeDetection()` (a further 34.59 kB off the bundle).

### Unbounded lookups and audit reads
**Priority:** P2
`CourseRepository.GetLookupAsync:200` returns every course with no keyword or `TOP`, unlike
`SearchPromotionsAsync` which caps at 20. `RowAuditRepository.cs:14` returns a record's entire history to
render one badge line.

### Inline cell edit costs two round trips and can silently revert other people's edits
**Priority:** P2
`course-list.component.ts:246` does `getById` then `update`, rewriting all eleven editable fields from a
possibly stale snapshot — so editing 時數 can overwrite someone else's 定價 while showing 已儲存. There
is no ETag/rowversion. A narrow `PATCH` would remove both problems.

### Unbounded request bodies
**Priority:** P3
`CourseRequest.Outline` and `TowardCertOrExam` have no `[StringLength]` and land in `nvarchar(max)`, so
any signed-in user can store megabytes per course, which the unpaged list read then materialises.

## Accessibility

### Form errors are never announced
**Priority:** P1
`aria-describedby`, `aria-invalid`, `role="alert"` and `aria-live` appear nowhere in the SPA. All 11
form templates render the message as a sibling `<small>`, so a screen-reader user hears the label and
nothing else. Fix the `/crud` template once and it lands on all 11.

### The mobile sidebar never closes
**Priority:** P1
`app.ts` sets `sidebarCollapsed` once at construction; nothing re-collapses it on navigation. Below
1024px the sidebar is a fixed overlay, so tapping a menu item navigates underneath it and the user must
find the hamburger again every time. There is also no scrim, no click-outside, no Escape and no focus
trap.

### Inline cell editing is mouse-only
**Priority:** P2
The ten editable `<td>`s bind `(dblclick)` with no `tabindex` and no keyboard opener, so keyboard users
cannot reach it at all; on touch, double-tap is a zoom gesture. The only affordance is a hover outline,
which does not exist on touch.

### No skip link
**Priority:** P2
Up to 11 sidebar links precede `<main>` on every route, `<main>` has no id or label, and focus never
moves on navigation.

### Disabled save button with no visible reason
**Priority:** P3
Nine forms disable 儲存 on `form.invalid` while gating messages on `touched || dirty`, so a user who has
touched nothing sees a dead button and no explanation. Keep it enabled and `markAllAsTouched()` on
submit.

### Audit badge hides its own summary from screen readers
**Priority:** P3
`row-audit-badge.component.ts:40` sets `ariaLabel="異動紀錄 History"`, which replaces the button's
content — so the who-and-when summary, the whole point of the badge, is never announced.

## Design / UX

### No error state on any list page
**Priority:** P2
The scaffold's error handler shows a toast and leaves `items` empty, so a failed load is indistinguishable
from an empty table once the toast fades, with no retry. `course-print.component.html:5` already
distinguishes failure from not-found — copy that.

### Empty state cannot tell "no rows" from "no matches"
**Priority:** P3
All seven tables show 沒有符合條件的資料。 even with no filter applied, and offer no action.

### Three different loading treatments
**Priority:** P3
Table overlay, an inline 載入中… in the promo toolbar, and a `<p class="state">` on detail pages.

### Dead dark-mode configuration
**Priority:** P3
`app.config.ts:27` declares `darkModeSelector: '.app-dark'`, but nothing ever adds that class and
surface colours are hardcoded, so the switch could not be flipped safely. Either finish it or drop it.

### Print view has no screen breakpoint
**Priority:** P3
`course-print.component.scss` keeps a 15mm sheet padding and a non-wrapping head with a 42mm QR aside on
screen, leaving roughly 48px for the course title on a 375px phone.

### Promo board truncates descriptions with no way to read them
**Priority:** P3
`.row__cell` ellipsises with no `title` or tooltip; 16rem holds about 16 Chinese characters.

## Tests

### Eight of eleven repositories have no test
**Priority:** P1
Only Partner, Course and RowAudit run on the `RecordingDbConnection` harness (which needs no SQL Server).
Untested: AppUser, AppRole, Auth, Certification, CourseGroup, FeaturedPromoItem, Lookup, PublishStatus —
roughly 1,400 lines of SQL, including `AuthRepository`, whose SQL is where the token-revocation contract
actually lives.

### SQL error translation is never exercised
**Priority:** P1
No test constructs a `SqlException`; all ten 409 tests throw the domain exception from a mock. A
repository that dropped its `catch` or matched the wrong number would return 500 in production with a
green suite. Extract the number checks into testable predicates (`SqlErrorNumbers` now exists) and unit
test the mapping.

### `FeaturedPromoItemRepository.SwapSlotAsync` is untested and deadlock-prone
**Priority:** P1
Two overlapping swaps on the same day and centre deadlock deterministically; unlike Create and Update it
catches neither 1205 nor the unique violation, so it surfaces as a bare 500. `TemporarySlot = 0` is also
a legal `tinyint` with no CHECK constraint, so a stray slot-0 row breaks every swap for that day.

### Missing specs
**Priority:** P2
`LookupService` (the only Angular service without one — ten endpoint builders), `session-storage.util.ts`
(its try/catch fallbacks are what every list page depends on), `ascii.validator.ts`, `app.config.ts` DI
wiring, and the `DateOnly` Dapper type handler.

### Open-redirect guard is half covered
**Priority:** P2
`login.component.ts:73` guards both `startsWith('/')` and `!startsWith('//')`, but the only negative test
uses `https://evil.example/phish`, which the first half rejects alone. Deleting the second half leaves
the suite green. Add `//evil.example`, `///evil.example`, `/\evil.example`.

### Time-dependent and timer-dependent tests
**Priority:** P3
`FeaturedPromoItemsControllerTests.cs:86` reads the machine clock on both sides instead of the injected
`TimeProvider`, so it fails across local midnight into a Monday. Four QR specs wait on
`setTimeout(resolve, 0)` for a real canvas rasterisation rather than awaiting the work.
`app.routes.spec.ts:24` uses Jasmine `fail()` as if it threw, so a missing route reports a
`TypeError` instead of the intended message.

### `sessionStorage` failure leaves a half-signed-in app
**Priority:** P2
`writeSession` swallows the exception but `accessToken()` reads only from storage, so with site data
blocked, login returns 200, `isAuthenticated()` is true, and every request goes out with no
`Authorization` header. Make the signal the source of truth and storage the cache.

## Code health

### A stale second copy of the /crud generator
**Priority:** P2
`skills/crud/SKILL.md` is 12 lines behind `.claude/skills/crud/SKILL.md` (the one actually loaded) and
still documents the old audit wiring. This is the artifact that produced all eight features, so whoever
opens the wrong copy scaffolds feature nine without it. Delete it or make it a pointer.

### Eight copies of the list-page scaffold
**Priority:** P3
Each list component re-declares `SortState`/`PageState`, `rowsPerPageOptions`, and byte-identical
`onSort`/`onPage`/`activeFilterCount`/`filterBadge` (~60 lines × 7), and each list stylesheet re-declares
~90 lines of page chrome. That duplication has already caused one shipped visual bug, documented in
`styles.scss:24-27`. The delete-confirmation dialog config is copy-pasted 15 times. Extract shared
helpers and fix the `/crud` templates so feature nine does not repeat it.

### `GET /api/{entity}` is dead in all eight features
**Priority:** P3
No SPA code calls `getAll()`; every list uses `query()` and dropdowns use `/api/lookups/*`. That leaves
eight unpaged select-everything endpoints exposed and eight tests existing only to keep dead code green.
(`deploy.ps1` smoke-probes `GET /api/publish-statuses`, so keep or replace that one.)

### Create responses are rebuilt by hand
**Priority:** P3
Every scaffolded `Create` re-lists the entity's columns to construct the 201 body, while the repository
already loaded the real row to hand to the audit writer and then discards it. Return that row instead —
the current body silently omits any database-computed default.

### Smaller items
**Priority:** P4
- Feature base paths are hardcoded string literals in 11-17 places each; `auth.service.ts` already shows
  the constant idiom.
- The 1023px sidebar breakpoint is duplicated between `app.ts:126` and `app.scss:123`, with comments in
  both admitting the coupling and nothing enforcing it.
- `removeSession` is exported and never called; `partner.model.ts` re-exports `asciiValidator` for a
  single in-tree importer; three forms wrap one observable in `forkJoin`.
- `AuditHelper.ValuesEqual` uses order-sensitive `SequenceEqual` while its own comment promises
  order-insensitive comparison, so a reordered role list is audited as a change that did not happen.
- `RowAudit.ActionDesc` is `varchar(1000)` receiving Traditional Chinese — under a non-CJK collation
  every character becomes `?`, and since the audit shares the caller's transaction, an over-length value
  rolls back the user's otherwise valid save.
- `WeekRange.For` overflows on `date=9999-12-31`, returning an unhandled 500.
- FK violations on create/update return 500; only DELETE catches 547.
- `SigningKeyCache` records only successful loads, so during a database stall every request queues on one
  semaphore and takes its own connection timeout, turning a brief blip into a long outage.
- `AuthController.cs:78` returns config detail (table, key and property names) from the anonymous login
  endpoint.
- The auth interceptor attaches the bearer token to every outgoing request with no origin check, and
  never checks `exp` client-side.
- `deploy\` has no automated verification at all, and IIS is not installed on the dev box.

## Completed

### Break the audit-trail → default-password → account-takeover chain
**Completed:** v1.0.0.0 (2026-09-10)
`GET /api/row-audits` now requires the Admin role for `tableName=AppUser|AppRole`, and the reset audit no
longer records that the password was set to the system default.

### Make an administrator's password reset actually end the target's session
**Completed:** v1.0.0.0 (2026-09-10)
`AppUsersController.ResetPassword` invalidates the cached password stamp, and `PasswordUpdatedTime` is
now written from the same `TimeProvider` that stamps the token instead of SQL Server's `GETUTCDATE()`.

### Return 409 instead of 500 when two creates race
**Completed:** v1.0.0.0 (2026-09-10)
AppRole, AppUser and PublishStatus map SQL 2627/2601 to `DuplicateKeyException` → 409.

### Fix the null-role-list 500
**Completed:** v1.0.0.0 (2026-09-10)
`AppRoleRepository.Normalize` null-guards like its three siblings.

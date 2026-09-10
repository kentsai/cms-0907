# Force a password change for users who sign in with the default password

> **Status:** implemented as planned in commit `7e7354c` (2026-09-08, `develop`). Spec: `spec\auth\Auth.md`
> (*Default-password lock*, *Forced change after a default-password login*). The plan below is the approved
> design as written before implementation; two details settled during the build are worth knowing:
> the JWT claim is written as a JSON boolean (the SPA accepts `true` or `"true"`), and the profile spec's
> 變更密碼 tests moved to `change-password-form.component.spec.ts` with the shared form.

## Context

Admins create accounts and reset passwords to `SysConfig.appConfig.defaultPassword`
(`spec\auth\AppUser.md`). Today such a user can sign in and use the whole system with that shared
password indefinitely. Requirement: when a login succeeds **with the default password**, the user must
change it before anything else works. Enforcement has to be server-side (the API must refuse other
calls), with the Angular app steering the user to a change-password page.

There is no `MustChangePassword` column in `AppUser` (`database\auth.sql`) and no local DB, so the
flag is **derived at login and carried in the JWT** rather than stored. Detection at login also covers
both paths (create and admin reset) for free, and the existing revocation mechanism already ends the
flagged session once the password changes (`PasswordUpdatedTime` > token `iat`).

## Design (recommended)

**Backend**
1. `AuthController.Login` — after `CredentialsMatch`, read the default password
   (`IAuthRepository.GetDefaultPasswordAsync`, same shape as `GetSymmetricSecurityKeyAsync`, using
   `AppConfigJson.ExtractDefaultPassword`) and set `mustChangePassword =
   string.Equals(request.Password, defaultPassword, Ordinal)` (the hash already matched, so a plaintext
   compare is equivalent; `AppConfigException` → existing 500 path). Pass it to
   `IJwtTokenIssuer.Issue(user, roleIds, key, mustChangePassword)` which adds claim
   `JwtTokenIssuer.MustChangePasswordClaim = "mustChangePassword"` = `"true"` **only when true**.
   `LoginResponse` gains `bool MustChangePassword` too (cheap, explicit for API clients; the SPA reads
   the claim from the token so session storage keeps its shape).
2. New `Infrastructure\PasswordChangeRequiredFilter : IAuthorizationFilter`, registered globally in
   `Program.cs` right after the `AuthorizeFilter`: if the (authenticated) principal has the claim and
   the endpoint does not carry the new `[AllowPasswordChangeRequired]` attribute → `403`
   `{ message: "請先變更密碼後再使用系統。" }` (`PasswordChangeRequiredMessage`). Anonymous
   `Login` has no claim so it is unaffected. Only `AuthController.ChangePassword` gets the attribute
   (`UpdateProfile` and everything else stay blocked). 403, not 401, so the SPA does not log the
   user out. Attribute lives in `Infrastructure\AllowPasswordChangeRequiredAttribute.cs`.
3. `ChangePassword` itself is unchanged: current password = default, new one policy-checked, stamp
   updated → the flagged token is dead on the next request, and the next login (new password) issues
   a clean token. Admin `reset-password` unchanged (next login with the default is flagged).
   Pre-existing tokens without the claim keep working until they expire (no migration needed).

**Frontend**
1. `core/utils/jwt.util.ts` — `mustChangePasswordFromToken(token)` (claim `mustChangePassword`
   equals `'true'` or `true`). `AuthService` gets `readonly mustChangePassword = computed(...)` and
   constant `CHANGE_PASSWORD_PATH = '/change-password'`.
2. Extract the 變更密碼 form out of `ProfileComponent` into a reusable standalone
   `features/auth/change-password-form/change-password-form.component` (inputs: none; it owns the
   `passwordForm`, `passwordError`, `applyServerErrors`, the `changePassword()` call, the clear +
   toast + `/login?reason=password-changed` redirect). `ProfileComponent` keeps the profile card and
   renders `<app-change-password-form>` inside its second card; the message constants move with it.
3. New page `features/auth/change-password/change-password.component` at route `change-password`
   inside the guarded group (`app.routes.ts`): a `p-message severity="warn"` notice
   「您目前使用預設密碼登入，請先變更密碼後再繼續使用系統。（You signed in with the default password.
   Please change it before continuing.）」 shown when `auth.mustChangePassword()`, then the shared form
   in a `p-card`. Reachable by anyone signed in (no notice when not flagged).
4. `authGuard` — after the token check: if `auth.mustChangePassword()` and the target URL is not
   `/change-password` → `createUrlTree([CHANGE_PASSWORD_PATH])`. `LoginComponent.submit` success →
   `/change-password` when flagged (ignores `returnUrl`), else current behaviour.
5. `authInterceptor` — a **403** while `auth.mustChangePassword()` → `router.navigateByUrl('/change-password')`
   (fallback; error still re-thrown). 401 handling unchanged.
6. Shell (`app.html` / `app.ts`) — while flagged: no sidebar and the topbar name is plain text (no
   `/profile` link); 登出 stays. Add `app-shell--locked` class (grid `1fr` column like collapsed) in
   `app.scss`; expose `auth.mustChangePassword` in the template.

## Files

| Area | Change |
|---|---|
| `src\CMS.API\Repositories\IAuthRepository.cs`, `AuthRepository.cs` | `GetDefaultPasswordAsync` (reuse the `appConfig` SELECT + `AppConfigJson.ExtractDefaultPassword`) |
| `src\CMS.API\Infrastructure\JwtTokenIssuer.cs` | `MustChangePasswordClaim`, extra `bool` parameter on `Issue` |
| `src\CMS.API\Infrastructure\PasswordChangeRequiredFilter.cs`, `AllowPasswordChangeRequiredAttribute.cs` | new |
| `src\CMS.API\Controllers\AuthController.cs` | detection in `Login`, attribute on `ChangePassword` |
| `src\CMS.API\Models\LoginResponse.cs` | `MustChangePassword` |
| `src\CMS.API\Program.cs` | register the filter after `AuthorizeFilter` |
| `src\CMS.NG\src\app\core\{utils\jwt.util.ts, services\auth.service.ts, guards\auth.guard.ts, interceptors\auth.interceptor.ts, models\auth.model.ts}` | claim helper, signal, redirect, 403 handling, `mustChangePassword` on `UserProfile`-returning login response type |
| `src\CMS.NG\src\app\features\auth\change-password-form\*` (new), `features\auth\change-password\*` (new), `features\auth\profile\*`, `features\auth\login\login.component.ts` | form extraction, forced page, login redirect |
| `src\CMS.NG\src\app\app.{ts,html,scss,routes.ts}` | locked shell, route |
| `src\CMS.NG\src\app\testing\auth-testing.ts` | `fakeProfile(..., mustChangePassword)` / `seedSignedInUser` option |
| `spec\auth\Auth.md`, `docs\claude\feature-status.md`, `CLAUDE.md` (auth rule line) | document the flow, claim, 403, new route, test totals |

## Tests

Backend (`src\CMS.API.Tests`):
- `AuthControllerTests`: login with the default password → claim present + `MustChangePassword = true`;
  another password → no claim / false; default-password config missing → 500 (existing `Problem` path);
  `GetDefaultPasswordAsync` never called on failed credentials.
- `JwtTokenIssuerTests`: claim emitted only when requested.
- New `PasswordChangeRequiredFilterTests` (unit, `AuthorizationFilterContext`): flagged + no attribute
  → 403 body; flagged + attribute → passes; unflagged / anonymous → passes.
- `JwtBearerAuthorizationTests` via `CmsApiFactory` (add `GetDefaultPasswordAsync` setup, e.g.
  `DefaultPassword = "Cms@2026"` and a second credential path): login with default → `GET
  /api/publish-statuses` 403 and `PUT /api/auth/profile` 403; `POST change-password` 204 → old token
  401 → new login has no claim and 200. Reflection test: `ChangePassword` is the only action with
  `[AllowPasswordChangeRequired]`.

Frontend (Karma): `jwt.util.spec` (claim helper), `auth.service.spec` (`mustChangePassword`),
`auth.guard.spec` (redirect to `/change-password`, allowed on that route), `auth.interceptor.spec`
(403 navigates when flagged, ignored otherwise), `login.component.spec` (flagged → `/change-password`
even with `returnUrl`), new `change-password-form.component.spec` (the 變更密碼 cases moved from
`profile.component.spec`), new `change-password.component.spec` (notice shown/hidden), `app.spec`
(locked: no sidebar, no profile link, logout present).

## Verification

```powershell
dotnet test C:\dev\cms\src\CMS.sln
cd C:\dev\cms\src\CMS.NG; npx ng test --watch=false --browsers=ChromeHeadless
cd C:\dev\cms\src\CMS.NG; npx ng build
```
Build the API with `-p:ArtifactsPath=<scratchpad>` if `CMS.API.exe` is running. No local DB, so the
end-to-end flow is covered by the `CmsApiFactory` integration test rather than a manual login.

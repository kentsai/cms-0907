# Build Spec for Authentication & Authorization (Auth)
- database schema: `.\database\auth.sql` — `AppUser`, `AppUserRole`, `SysConfig` (no dedicated table; this spec
  covers the login / session / self-service flows that sit on top of `AppUser`)
- derived from the implementation in the working tree on 2026-09-08 (Login, JWT bearer, My Profile, Change Password)
- related spec: `spec\auth\AppUser.md` (admin CRUD of accounts, default-password reset)

---

## Summary

The API issues **HS256 JWTs** on login and requires one on every other request. Passwords are stored as
**SHA-256 hex** in `AppUser.PasswordHash` and never leave the server. The signing secret and the default password
both live in the JSON of `SysConfig.configValue WHERE configKey = 'appConfig'`. Authorization is
**authentication-only**: every action except `POST /api/auth/login` needs a valid token, and no action checks a
role. The single role-aware behaviour is the Angular shell hiding the 系統管理 Admin menu group from users whose
token has no `Admin` role. A password change (self-service or admin reset) invalidates every token issued
before it, so the user must log in again with the new password.

| Item | Detail |
|------|--------|
| Credential row | `AppUser` (`UserId` string PK, `UserName`, `IsActive`, `PasswordHash`, `PasswordUpdatedTime`) |
| Roles | `AppUserRole.RoleId` per user → one `role` claim each |
| Secrets | `SysConfig.appConfig.symmetricSecurityKey` (≥ 32 UTF-8 bytes), `SysConfig.appConfig.defaultPassword` |
| Token | HS256, issuer `CMS.API`, no audience, lifetime **24 h**, claims `sub` / `jti` / `iat` / `userId` / `userName` / `role`* |
| Session (browser) | `sessionStorage['auth-profile']` = `{ userId, userName, accessToken }`; gone when the tab closes |
| Authorization | Global `AuthorizeFilter` (authenticated user); `[AllowAnonymous]` only on `AuthController.Login` |
| Revocation | Token `iat` < `AppUser.PasswordUpdatedTime` (whole seconds) → 401 |

---

## Localization

### Chinese Names

- Login page: CMS 登入 (帳號, 密碼, 登入)
- My Profile page: 個人資料 My Profile (帳號 UserId, 姓名 UserName, 角色 Roles, 還原, 儲存)
- Change Password card: 變更密碼 Change Password (目前密碼, 新密碼, 確認新密碼, 變更密碼)
- Shell: 登出; Admin-only menu group 系統管理 Admin

### Messages (API constants in `AuthController` / `PasswordPolicy` / `ConfigureJwtBearerOptions`)

| Constant | Text | Where |
|----------|------|-------|
| `InvalidCredentialsMessage` | 帳號或密碼錯誤。 | Login 401 body `{ message }` |
| `UserNameRequiredMessage` | 請輸入姓名。 | Profile 400 `errors.UserName` |
| `CurrentPasswordRequiredMessage` | 請輸入目前密碼。 | Change password 400 `errors.CurrentPassword` |
| `CurrentPasswordIncorrectMessage` | 目前密碼錯誤。 | Change password 400 `errors.CurrentPassword` |
| `NewPasswordRequiredMessage` | 請輸入新密碼。 | Change password 400 `errors.NewPassword` |
| `PasswordPolicy.Message` | 密碼長度至少需 8 碼，且內容須至少包含四種字元的其中三種：大寫英文／小寫英文／數字／符號 | Change password 400 `errors.NewPassword` |
| `ConfirmPasswordMismatchMessage` | 新密碼與確認新密碼不一致。 | Change password 400 `errors.ConfirmNewPassword` |
| AppConfig error title | 系統設定錯誤 | 500 `ProblemDetails.title` when `appConfig` is unusable |
| `PasswordChangedFailureMessage` | 密碼已變更，請重新登入。 | Bearer `context.Fail` reason (401) |
| `UnknownUserFailureMessage` | 使用者不存在，請重新登入。 | Bearer `context.Fail` reason (401) |

Frontend-only copy: login notice 密碼已變更，請使用新密碼重新登入。（Your password was changed. Please sign in
again with the new password.）; the password rule is shown bilingually (Chinese rule + English gloss) under the
new-password field; field-required texts are bilingual as well.

---

## Password storage & policy

1. `PasswordHash` = `PasswordHasher.Sha256Hex(password)`: SHA-256 of the UTF-8 bytes, lowercase hex, 64 chars.
   Comparison is **constant-time** (`CryptographicOperations.FixedTimeEquals`) and **hex-case-insensitive**
   (the stored value is trimmed and lower-cased first).
2. `PasswordHash` appears in exactly one SELECT (`IAuthRepository.GetCredentialAsync`) and in no wire model:
   `LoginResponse`, `ProfileResponse`, `AppUser` and every Angular model have no hash member.
3. Complexity (`Infrastructure\PasswordPolicy`, mirrored by `core/utils/password.validator.ts`):
   length ≥ **8** and at least **3 of 4** classes — ASCII uppercase `A-Z`, ASCII lowercase `a-z`, ASCII digit
   `0-9`, ASCII symbol (any other printable ASCII: `!`–`/`, `:`–`@`, `[`–`` ` ``, `{`–`~`). Whitespace, CJK and
   full-width characters are allowed but count towards no class. Applies to self-service changes only; the
   admin default password (`AppUser.md`) is not policy-checked.
4. `PasswordUpdatedTime` is UTC (`GETUTCDATE()` on create/reset, `TimeProvider.GetUtcNow()` on self-service
   change) and doubles as the **token revocation stamp** (see below).

---

## Login

`POST /api/auth/login` — `[AllowAnonymous]`, body `LoginRequest { userId, password }` (both `[Required]`,
`userId` ≤ 200).

1. `GetCredentialAsync(userId)` loads `UserId, UserName, IsActive, PasswordHash`.
2. The user is accepted only if **all** hold: row exists; `UserId` equals the request **ordinally** (the DB
   collation may be case-insensitive, the API is not); `IsActive = 1`; SHA-256(password) matches the hash.
   Every failure returns the same **401** `{ "message": "帳號或密碼錯誤。" }` — callers cannot tell which check
   failed.
3. On success: `GetRoleIdsAsync(userId)` (ordered by `RoleId`), `GetSymmetricSecurityKeyAsync()` (read from
   `SysConfig` **per call**, so a rotated key is used without restart), then `IJwtTokenIssuer.Issue`.
4. Response **200** `LoginResponse { userId, userName, accessToken }`.
5. `AppConfigException` (missing `appConfig` row, invalid JSON, missing/empty/too-short key) → **500**
   `ProblemDetails { title: "系統設定錯誤", detail }`.

### Token (`Infrastructure\JwtTokenIssuer`)

| Claim | Value |
|-------|-------|
| `iss` | `CMS.API` |
| `sub`, `userId` | `AppUser.UserId` |
| `userName` | `AppUser.UserName` at issue time (never refreshed; nothing reads it server-side) |
| `role` (0..n) | one per `AppUserRole.RoleId` (`ClaimTypes.Role`) |
| `jti` | random GUID |
| `iat`, `nbf` | issue instant (UTC, whole seconds) |
| `exp` | `iat` + 24 h |

Signed HS256 with the UTF-8 bytes of `symmetricSecurityKey`; a key shorter than 32 bytes throws
`AppConfigException`. The clock is `TimeProvider` (pinned in tests).

---

## Bearer authentication (every other request)

`Program.cs`: `AddAuthentication(JwtBearer).AddJwtBearer()` + `ConfigureOptions<ConfigureJwtBearerOptions>()`;
MVC gets a global `AuthorizeFilter(RequireAuthenticatedUser)`. Swagger declares the `Bearer` security scheme.

Validation parameters: issuer `CMS.API`, no audience, lifetime with **1 min** clock skew, signature against
`ISigningKeyCache.CurrentKeys`, `NameClaimType = userId` (so `User.Identity.Name` and `RowAuditWriter` record
the UserId), `RoleClaimType = ClaimTypes.Role`.

Events:

1. `OnMessageReceived` → `SigningKeyCache.RefreshAsync`: re-reads the key from `SysConfig` when the cached copy
   is older than **1 min**. A transient error keeps the last good key (logged); an `AppConfigException`
   clears the keys, so every protected request is 401 until the config is fixed.
2. `OnTokenValidated` → password-change revocation:
   - `userId` claim missing → fail.
   - `IPasswordStampCache.GetAsync(userId)` (singleton, per-user cache, **1 min** TTL, backed by
     `IAuthRepository.GetPasswordStampAsync` = `SELECT PasswordUpdatedTime FROM AppUser WHERE UserId = @UserId`).
   - No row → fail (user deleted). `PasswordUpdatedTime` null → accept.
   - `PasswordStampCache.IsIssuedBeforePasswordChange(iat, stamp)`: compares **whole seconds**; `iat` earlier
     than the stamp → fail. A token issued in the same second as the change is still accepted, so a login
     immediately after the change is never rejected.
   - Lookup exception (DB down) → logged, token **kept** (signature and lifetime already passed).

Any failure → **401** with `WWW-Authenticate: Bearer`; the repository behind the endpoint is never called.
Anonymous requests to `/api/auth/login` ignore a bad/stale `Authorization` header.

### Authorization rules

- `AuthController.Login` is the **only** `[AllowAnonymous]` action in the assembly; no controller carries a
  class-level `[AllowAnonymous]` (enforced by a reflection test).
- No `[Authorize(Roles = …)]` anywhere: an authenticated user may call every endpoint, including the
  `系統管理` CRUD (AppUser, AppRole, reset-password). Role claims are informational for the API.
- Frontend: `authGuard` (`canActivateChild` on the whole app group) requires a stored token and otherwise
  redirects to `/login?returnUrl=<attempted url>`; only in-app absolute paths (`/…`, not `//…`) are honoured
  after login. `AuthService.isAdmin()` (token has role `Admin`) controls the 系統管理 Admin sidebar group only.

---

## My Profile

`PUT /api/auth/profile` — body `UpdateProfileRequest { userName }` (`[Required]`, ≤ 200). The user is the
token's `userId` claim; the request type has **no** `UserId` / `RoleIds` member, so such JSON is ignored.

1. No `userId` claim → 401.
2. `userName` trimmed; empty → **400** `ValidationProblem` `errors.UserName = ["請輸入姓名。"]`.
3. `UpdateUserNameAsync(userId, userName)`: transaction, `SELECT UserName` (no row → **404**),
   `UPDATE AppUser SET UserName = @UserName WHERE UserId = @UserId`, RowAudit `UPDATE` on `AppUser` with
   `ActionDesc = "UserName (profile)"` (or `"(no changes)"` when identical), commit.
4. **200** `ProfileResponse { userId, userName, roles }` — roles echo the token; `IsActive`, roles and the
   password are untouchable here.

Frontend `/profile`: UserId read-only, roles as read-only `p-tag`s from the token, editable UserName (required,
≤ 200, not whitespace-only). 儲存 PUTs `{ userName }` only and patches the stored profile (session storage +
signals → topbar). 400 shows the API message; 404 → 找不到使用者資料。; else 儲存失敗，請稍後再試。.

---

## Change Password (self-service)

`POST /api/auth/change-password` — body `ChangePasswordRequest { currentPassword, newPassword, confirmNewPassword }`
(all `[Required]`); the user is the token's `userId` claim (no `UserId` member). Checks run **in this order**
and stop at the first failure; every rejection is **400** `ValidationProblem` with exactly one `errors` key:

| # | Check | On failure |
|---|-------|-----------|
| 0 | `userId` claim present | 401 |
| 1 | `currentPassword` non-empty | `CurrentPassword` = 請輸入目前密碼。 (no DB read) |
| 2 | `GetCredentialAsync(userId)` finds the row | 404 |
| 3 | SHA-256(currentPassword) matches `PasswordHash` (constant-time, hex-case-insensitive) | `CurrentPassword` = 目前密碼錯誤。 — **nothing is written** |
| 4 | `newPassword` non-empty | `NewPassword` = 請輸入新密碼。 |
| 5 | `PasswordPolicy.IsCompliant(newPassword)` | `NewPassword` = `PasswordPolicy.Message` |
| 6 | `newPassword == confirmNewPassword` (ordinal, no trimming) | `ConfirmNewPassword` = 新密碼與確認新密碼不一致。 |

Success: `UpdatePasswordAsync(userId, Sha256Hex(newPassword), TimeProvider.GetUtcNow())` — transaction,
`SELECT COUNT(*)` (no row → **404**), `UPDATE AppUser SET PasswordHash = @PasswordHash, PasswordUpdatedTime =
@PasswordUpdatedTime WHERE UserId = @UserId`, RowAudit `UPDATE` with `ActionDesc = "PasswordHash (changed by
user)"`, commit; then `IPasswordStampCache.Invalidate(userId)` and **204**. `IsActive` is not re-checked (the
token already proves an active login). The new password may equal the old one (no history rule).

**Consequence:** the token that made the request — and every other token issued before the new
`PasswordUpdatedTime` — is rejected on the very next request. The admin reset in `AppUser.md` has the same
effect within the 1-minute stamp cache (it does not call `Invalidate`).

Frontend (`/profile`, second card 變更密碼): three `type="password"` inputs with `autocomplete`
`current-password` / `new-password` / `new-password`. Client validation mirrors the API (required ×3,
`passwordPolicyValidator` on 新密碼, group `passwordsMatchValidator`); the submit button stays disabled while
invalid and the API is never called for empty / weak / mismatched input. 400 `errors` are pinned under the
matching input (`{ server: message }`; a server policy error is shown with the bilingual copy). On **204** the
page calls `AuthService.clear()` (session storage wiped, signals reset), toasts 密碼已變更, and navigates to
`/login?reason=password-changed`; the login page shows the bilingual notice for that `reason` and ignores any
other value.

---

## Frontend session handling

- `AuthService` (`core/services/auth.service.ts`): `login()` POSTs and stores the `UserProfile` under
  `AUTH_PROFILE_KEY = 'auth-profile'` in **sessionStorage** (never localStorage); `profile`, `isAuthenticated`,
  `userName`, `roles` (decoded from the token, no API call; `.NET` emits one role as a string, several as an
  array), `isAdmin` signals; `accessToken()` reads storage directly so the interceptor/guard see a clear made
  elsewhere; `clear()` wipes **all** of sessionStorage (profile and remembered list filters); `logout()` =
  `clear()` + `/login`.
- `authInterceptor`: adds `Authorization: Bearer <token>` to every request; a **401** from any URL except the
  login URL → `logout()` (so a revoked or expired token drops the user on `/login` on their next call).
- `authGuard`: see Authorization rules.
- Shell (`app.ts`): topbar shows `userName` linking to `/profile` and a 登出 button; sidebar menu filtered by
  `isAdmin()`.
- The stored `userName` is patched after a profile save; the token itself is never refreshed. There is no
  expiry warning or silent renewal: after 24 h the next API call is 401 → login page.

---

## API Endpoints

| Method | Route | Auth | Body | Responses |
|--------|-------|------|------|-----------|
| `POST` | `/api/auth/login` | anonymous | `LoginRequest` | 200 `LoginResponse` · 400 (model) · 401 `{ message }` · 500 (appConfig) |
| `PUT` | `/api/auth/profile` | bearer | `UpdateProfileRequest` | 200 `ProfileResponse` · 400 `errors.UserName` · 401 · 404 |
| `POST` | `/api/auth/change-password` | bearer | `ChangePasswordRequest` | 204 · 400 `errors.{CurrentPassword\|NewPassword\|ConfirmNewPassword}` · 401 · 404 |
| any | every other `/api/*` | bearer | — | 401 + `WWW-Authenticate: Bearer` when the token is missing, malformed, wrongly signed, expired, revoked by a password change, or the user row is gone |

---

## Backend Notes

### Models (`CMS.API\Models`)

```csharp
public class LoginRequest { [Required, StringLength(200)] string UserId; [Required] string Password; }
public class LoginResponse { string UserId; string UserName; string AccessToken; }          // no hash, no roles (in token)
public class UpdateProfileRequest { [Required, StringLength(200)] string UserName; }        // no UserId / RoleIds
public class ProfileResponse { string UserId; string UserName; List<string> Roles; }
public class ChangePasswordRequest { [Required] string CurrentPassword, NewPassword, ConfirmNewPassword; } // no UserId
public sealed class AppUserCredential { string UserId, UserName; bool IsActive; string PasswordHash; } // backend only
public sealed class PasswordStamp { DateTime? PasswordUpdatedTime; }                                   // backend only
```

### Repository (`IAuthRepository` / `AuthRepository`)

| Method | SQL |
|--------|-----|
| `GetCredentialAsync(userId)` | `SELECT UserId, UserName, IsActive, PasswordHash FROM AppUser WHERE UserId = @UserId` (the only PasswordHash read) |
| `GetRoleIdsAsync(userId)` | `SELECT RoleId FROM AppUserRole WHERE UserId = @UserId ORDER BY RoleId` |
| `GetSymmetricSecurityKeyAsync()` | `SELECT configValue FROM SysConfig WHERE configKey = 'appConfig'` → `AppConfigJson.ExtractSymmetricSecurityKey` |
| `UpdateUserNameAsync(userId, userName)` | see My Profile |
| `UpdatePasswordAsync(userId, hash, utcNow)` | see Change Password |
| `GetPasswordStampAsync(userId)` | `SELECT PasswordUpdatedTime FROM AppUser WHERE UserId = @UserId` (Kind forced to UTC) |

### Infrastructure

| Type | Role |
|------|------|
| `PasswordHasher` | `Sha256Hex(string)` |
| `PasswordPolicy` | `MinLength = 8`, `MinCharacterClasses = 3`, `IsCompliant`, `Message` |
| `AppConfigJson` | `ExtractDefaultPassword`, `ExtractSymmetricSecurityKey` (throw `AppConfigException`) |
| `JwtTokenIssuer` (`IJwtTokenIssuer`) | builds the token; `Issuer`, `TokenLifetime`, `UserIdClaim`, `UserNameClaim` |
| `SigningKeyCache` (`ISigningKeyCache`) | singleton, `CurrentKeys`, `RefreshAsync`, `CacheDuration = 1 min` |
| `PasswordStampCache` (`IPasswordStampCache`) | singleton, `GetAsync`, `Invalidate`, `IsIssuedBeforePasswordChange`, `CacheDuration = 1 min` |
| `ConfigureJwtBearerOptions` | validation parameters + the two events above; `ClockSkew = 1 min` |
| `TimeProvider` | registered as `TimeProvider.System`; injected into the issuer, both caches and `AuthController` |

### RowAudit

| Action | `TableName` | `PrimaryKeyValues` | `ActionType` | `ActionDesc` |
|--------|-------------|--------------------|--------------|--------------|
| Profile rename | `AppUser` | `UserId` | `UPDATE` | `UserName (profile)` / `(no changes)` |
| Self-service password change | `AppUser` | `UserId` | `UPDATE` | `PasswordHash (changed by user)` |
| Login / token validation | — | — | — | not audited |

`UserName` on the audit row is the bearer `Identity.Name`, i.e. the caller's `UserId`.

---

## Frontend Notes

### Routes

| Route | Component | Guard |
|-------|-----------|-------|
| `/login` | `LoginComponent` (`features/auth/login`) | public; reads `returnUrl`, `reason` |
| `/profile` | `ProfileComponent` (`features/auth/profile`) | `authGuard` (like every other app route) |

### Models (`core/models/auth.model.ts`)

```ts
export interface LoginRequest { userId: string; password: string; }
export interface UserProfile { userId: string; userName: string; accessToken: string; }   // stored in sessionStorage
export interface UpdateProfileRequest { userName: string; }
export interface ProfileResponse { userId: string; userName: string; roles: string[]; }
export interface ChangePasswordRequest { currentPassword: string; newPassword: string; confirmNewPassword: string; }
```

### Constants

`AUTH_PROFILE_KEY = 'auth-profile'`, `LOGIN_PATH = '/login'`, `ADMIN_ROLE = 'Admin'`,
`PASSWORD_CHANGED_REASON = 'password-changed'` (`auth.service.ts`); `PASSWORD_MIN_LENGTH = 8`,
`PASSWORD_MIN_CLASSES = 3`, `PASSWORD_POLICY_MESSAGE` (`password.validator.ts`); `ADMIN_MENU_LABEL = '系統管理 Admin'`
(`app.ts`).

### Login page

Fields 帳號 (required, ≤ 200, `autocomplete="username"`, trimmed) and 密碼 (required,
`autocomplete="current-password"`). 401 → the API message (or 帳號或密碼錯誤。) and the password field is
reset; other errors → 登入失敗，請稍後再試。. Success → `returnUrl` (in-app only) or `/`. Info notice when
`reason=password-changed`.

### Test helpers (`src/app/testing/auth-testing.ts`)

`fakeJwt(payload)`, `fakeProfile(roles, userName, userId)`, `seedSignedInUser(roles, userName)` — seed
sessionStorage **before** the TestBed creates `AuthService`.

---

## Tests

### Backend (`CMS.API.Tests`)

- `Controllers\AuthControllerTests` — login success (token decoded and claims/expiry asserted with a pinned
  clock), unknown / inactive / wrong-password / wrong-case UserId all yield the identical 401 body, hex-case-
  insensitive hash match, 500 on `AppConfigException`, no hash in `LoginResponse`.
- `Controllers\AuthControllerProfileTests` — user from the principal only, trimming, 400 for blank, 404, 401.
- `Controllers\AuthControllerChangePasswordTests` — 38 cases: hash + timestamp written, stamp cache invalidated,
  accepted/rejected password matrix, wrong current password writes nothing and wins over a weak new password,
  mismatch variants, 401 / 404, action not `[AllowAnonymous]`.
- `Infrastructure\PasswordPolicyTests`, `PasswordHasherTests`, `AppConfigJsonTests`, `JwtTokenIssuerTests`,
  `SigningKeyCacheTests`, `PasswordStampCacheTests` (whole-second comparison, TTL, per-user, invalidate,
  failure not cached).
- `Infrastructure\JwtBearerAuthorizationTests` — real pipeline via `CmsApiFactory` (repositories mocked):
  401 without / wrong-key / expired / malformed token, 401 when the key cannot load, login anonymous and its
  token works, only-anonymous-action reflection check, profile ignores body `userId`, token before/after a
  password change, unknown user, stamp lookup failure keeps the session, and an end-to-end
  change → old token 401 → old password 401 → new password logs in.

### Frontend (Karma)

`auth.service.spec`, `auth.interceptor.spec`, `auth.guard.spec`, `jwt.util.spec`, `password.validator.spec`,
`login.component.spec` (incl. the password-changed notice), `profile.component.spec` (name save; change
password: empty / weak / mismatch never call the API, success clears the session and redirects, failure keeps
it), `app.spec` (menu filtering, topbar).

---

## Not built / known gaps

- **No endpoint-level authorization**: the API checks authentication only. Hiding 系統管理 Admin in the UI
  is a convenience, not a control — any signed-in user can call the admin endpoints directly.
- No token refresh, expiry warning, or logout-side revocation (a token stays valid for 24 h unless the
  password changes).
- No login throttling / lockout, no password history, and the admin default password is not policy-checked.
- SHA-256 without salt is inherited from the legacy data format (see `AppUser.md`).
- Multi-instance deployments: a password change made on another instance (or by SQL) takes effect within the
  1-minute `PasswordStampCache` TTL; a rotated signing key within the 1-minute `SigningKeyCache` TTL.

---

## Files

| Area | File |
|------|------|
| API | `Controllers\AuthController.cs` |
| API | `Models\LoginRequest.cs`, `LoginResponse.cs`, `UpdateProfileRequest.cs`, `ProfileResponse.cs`, `ChangePasswordRequest.cs`, `AppUserCredential.cs`, `PasswordStamp.cs` |
| API | `Repositories\IAuthRepository.cs`, `AuthRepository.cs` |
| API | `Infrastructure\PasswordHasher.cs`, `PasswordPolicy.cs`, `AppConfigJson.cs`, `AppConfigException.cs`, `JwtTokenIssuer.cs`, `SigningKeyCache.cs`, `PasswordStampCache.cs`, `ConfigureJwtBearerOptions.cs` |
| API | `Program.cs` (auth services, global filter, Swagger Bearer scheme) |
| Tests | `Controllers\AuthController*Tests.cs`, `Infrastructure\{CmsApiFactory,FixedTimeProvider,JwtBearerAuthorizationTests,JwtTokenIssuerTests,SigningKeyCacheTests,PasswordPolicyTests,PasswordStampCacheTests}.cs` |
| NG | `core\models\auth.model.ts`, `core\services\auth.service.ts`, `core\interceptors\auth.interceptor.ts`, `core\guards\auth.guard.ts`, `core\utils\jwt.util.ts`, `core\utils\password.validator.ts`, `core\utils\session-storage.util.ts` |
| NG | `features\auth\login\*`, `features\auth\profile\*`, `app.ts` / `app.html` / `app.routes.ts` / `app.config.ts`, `testing\auth-testing.ts` |

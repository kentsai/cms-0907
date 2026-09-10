# Build Spec for AppUser
- database schema: `.\database\auth.sql` (the same `AppUser` / `AppUserRole` / `SysConfig` DDL also appears in `admin.sql`)

---

## Summary

`AppUser` is the login account table. Like `AppRole`, it has a display-only identity `pkid`
while the **clustered primary key is the string `UserId`**. Roles are assigned through the
`AppUserRole` junction (N-N with `AppRole`), managed on the user form with a multi-select.
The `PasswordHash` column is **backend-only**: it is never exposed to or accepted from the
frontend, it is seeded from the system default password on create, and it is changed only by
a dedicated reset-password endpoint.

| Item | Detail |
|------|--------|
| Primary Key | `UserId` **nvarchar(200)** (string PK). `pkid` int IDENTITY exists but is display-only (主代碼). |
| Foreign Keys | None |
| Required Fields | `UserId`, `UserName`, `IsActive` (DB default 1); `PasswordHash` is required in the DB but server-generated |
| N-N Relationships | `AppUserRole` (`UserId`, `RoleId`) — AppUser ↔ AppRole |
| Primary-Foreign Links | 查看角色 → `/admin/app-roles?userId={userId}` (the AppRole list already accepts `userId`) |
| Query Filters | keyword (`UserId`, `UserName`); `IsActive` tri-state; `RoleId` (users holding a role) |
| Default Sort | `UserId ASC` |

---

## Localization

### Chinese Table Name

- AppUser: 使用者
- Description: 系統登入帳號（帳號、名稱、啟用狀態、角色指派）

### Chinese Column Names

- pkid: 主代碼
- UserId: 使用者代碼
- UserName: 使用者名稱
- IsActive: 啟用
- PasswordHash: （不顯示）密碼雜湊
- PasswordUpdatedTime: 密碼更新時間
- RoleCount (computed in SELECT): 角色數
- RoleIds (N-N): 角色

---

## Required Fields

Required (NOT NULL):
- `UserId` — nvarchar(200), **user-supplied string key**, immutable after create
- `UserName` — nvarchar(200)
- `IsActive` — bit, DB default `1` (`DF_AppUser_IsActive`); the form pre-checks it on new
- `PasswordHash` — nvarchar(800), **server-generated** (see PasswordHash rules)

Optional (nullable):
- `PasswordUpdatedTime` — datetime; read-only, written by the server on create and on reset

---

## PasswordHash rules (backend only)

1. `PasswordHash` is **excluded** from `AppUserRequest`, from the `AppUser` response model and
   from every Angular model. There is no form field and no API input for it.
2. **On CREATE** the repository reads `SysConfig.configValue WHERE configKey = 'appConfig'`,
   parses it as JSON, takes the `defaultPassword` string property, hashes it with
   **`PasswordHasher.Hash`** (salted PBKDF2-HMAC-SHA256, 210,000 iterations, 16-byte salt, stored as
   `pbkdf2-sha256$<iterations>$<salt>$<hash>`) and stores the result as `PasswordHash`. Never
   `Sha256Hex` — that is the legacy read-only format (`Auth.md`, *Password storage*).
   `PasswordUpdatedTime` is set from the injected **`TimeProvider`**, not `GETUTCDATE()`: the column is the
   token-revocation stamp and is compared against the token's `iat`, which `JwtTokenIssuer` takes from the
   same clock. SQL Server's clock trailing the web server's by a second sent that comparison the wrong way.
3. **On UPDATE** the SQL touches only `UserName` and `IsActive`; `PasswordHash` and
   `PasswordUpdatedTime` are never modified.
4. **Reset** — `POST /api/app-users/{id}/reset-password` re-applies rule 2 to an existing user
   (new hash of the current default password, `PasswordUpdatedTime` from `TimeProvider`), writes a
   RowAudit `UPDATE` row with `ActionDesc = "PasswordHash"` and returns 204. The controller then calls
   `IPasswordStampCache.Invalidate(userId)`, so the target's existing token is rejected on its very next
   request instead of surviving up to `PasswordStampCache.CacheDuration` — this reset is the one lever an
   administrator has to end a hijacked session. The description deliberately does **not** say the password
   was reset to the default: `RowAudit` is readable through the 異動紀錄 badge, and naming the default turned
   the trail into a list of accounts standing on the shared `SysConfig.appConfig.defaultPassword`, a login
   anyone could then complete. It is the same wording an ordinary password change produces.
5. A missing `appConfig` row, invalid JSON, or a missing/empty `defaultPassword` raises
   `AppConfigException`; the controller turns it into **500** with a Chinese message
   (系統設定錯誤). Create is rolled back in that case.
6. **Consequence for the user** (`Auth.md`, *Default-password lock*): the next login with the default
   password succeeds but is confined to changing the password (API 403 elsewhere, SPA `/change-password`)
   until a password of their own is set. Nothing here needs to flag the row — login detects it.

Helpers: `Infrastructure\PasswordHasher.Hash(string)` / `.Verify(...)` / `.IsLegacyFormat(...)` and
`Infrastructure\AppConfigJson.ExtractDefaultPassword(string? json)` are pure and unit-tested.
`PasswordHasher.Sha256Hex` still exists to *verify* legacy rows; nothing may store its output.

---

## Foreign Keys

**N/A**

---

## Foreign-Primary Links

**N/A**

---

## Primary-Foreign Links

`AppUserRole` is the only table referencing `AppUser.UserId` and is handled as N-N. The list
and detail pages offer a 查看角色 button (`pi pi-users`) → `/admin/app-roles?userId={userId}`,
which the existing AppRole list already treats as an incoming filter.

---

## N-N Relationships

### AppUser ↔ AppRole via `AppUserRole`

- **List**: column 角色數 = `(SELECT COUNT(*) FROM AppUserRole ur WHERE ur.UserId = u.UserId)`.
- **Detail**: assigned roles as tags labelled with `RoleName` from `GET /api/lookups/app-roles`.
- **Form (edit + new)**: card 角色 with `p-multiselect` (`[maxSelectedLabels]="9999"`,
  `[filter]="true"`, `appendTo="body"`, placeholder 選擇角色（可複選）).
- **Request field**: `RoleIds: List<string>`; `AppUser.RoleIds` populated on `GetByIdAsync` only.
- **Sync on save** (same transaction): `DELETE FROM AppUserRole WHERE UserId = @UserId` then
  one `INSERT` per distinct id.
- **Delete**: junction rows first, then the user (confirm message warns with the role count).

---

## Query Filters

- **keyword** — LIKE on `UserId`, `UserName`.
- **isActive** (bool?) — tri-state 全部／是／否.
- **roleId** (string?) — `EXISTS (SELECT 1 FROM AppUserRole ur WHERE ur.UserId = u.UserId AND ur.RoleId = @RoleId)`;
  `p-select` from `GET /api/lookups/app-roles`. Also accepted as incoming query param `roleId`
  on the list route, overriding the saved filter.

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/app-roles` | Exists | `StringLookupItem[]` id = RoleId, label = RoleName |
| `GET /api/lookups/app-users` | Exists — **moved** from `LookupRepository` to `AppUserRepository.GetLookupAsync` (same route, same shape) | `StringLookupItem[]` id = UserId, label = `UserName (UserId)` |

`ILookupRepository` keeps only the certification / job-category lookups.

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/app-users` | List all, `ORDER BY UserId ASC`, includes `RoleCount` |
| `POST` | `/api/app-users/query` | Filtered query (body: `AppUserQuery`) |
| `GET` | `/api/app-users/{id}` | Get by **UserId** (string, no `:int`) → 200 (with `RoleIds`) / 404 |
| `POST` | `/api/app-users` | Create with default-password hash → 201; **409** if `UserId` exists (pre-check) or if a concurrent create wins the race (`DuplicateKeyException` from SQL 2627 / 2601); **500** if appConfig is unusable |
| `PUT` | `/api/app-users` | Update `UserName` / `IsActive` + roles (UserId from body, immutable) → 204 / 404 |
| `DELETE` | `/api/app-users/{id}` | Delete (junction rows first) → 204 / 404 / 409 |
| `POST` | `/api/app-users/{id}/reset-password` | Reset to the default password, then invalidate the target's password stamp → 204 / 404 / 500 |

Auth: a Bearer JWT is required on every action by the global `AuthorizeFilter`, and `AppUsersController`
plus the `/api/lookups/app-users` lookup additionally carry
`[Authorize(Policy = AuthorizationPolicies.Admin)]` — a signed-in non-administrator gets 403 and the
repository is never reached. The SPA mirrors it with `adminGuard` on `/admin/app-users`. The account's audit
trail is guarded too: `GET /api/row-audits?tableName=AppUser` needs the same role (`Auth.md`,
*Authorization rules*).

---

## Backend Notes

### Models

```csharp
public class AppUser
{
    public int Pkid { get; set; }
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime? PasswordUpdatedTime { get; set; }   // read-only
    public int RoleCount { get; set; }                   // subquery
    public List<string> RoleIds { get; set; } = [];      // GET by id only
    // NO PasswordHash
}

public class AppUserRequest
{
    [Required(AllowEmptyStrings = false), StringLength(200)] public string UserId { get; set; } = "";
    [Required(AllowEmptyStrings = false), StringLength(200)] public string UserName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<string> RoleIds { get; set; } = [];
    // NO PasswordHash
}

public class AppUserQuery { public string? Keyword; public bool? IsActive; public string? RoleId; }
```

### SQL — SELECT

```sql
SELECT u.pkid, u.UserId, u.UserName, u.IsActive, u.PasswordUpdatedTime,
       (SELECT COUNT(*) FROM AppUserRole ur WHERE ur.UserId = u.UserId) AS RoleCount
FROM AppUser u
-- QueryAsync WHERE:
--   (@Keyword IS NULL OR u.UserId LIKE '%' + @Keyword + '%' OR u.UserName LIKE '%' + @Keyword + '%')
--   AND (@IsActive IS NULL OR u.IsActive = @IsActive)
--   AND (@RoleId IS NULL OR EXISTS (SELECT 1 FROM AppUserRole ur WHERE ur.UserId = u.UserId AND ur.RoleId = @RoleId))
ORDER BY u.UserId ASC
-- GetById additionally: SELECT RoleId FROM AppUserRole WHERE UserId = @UserId ORDER BY RoleId
```

### SQL — INSERT

```sql
-- @PasswordHash = PasswordHasher.Hash of SysConfig.appConfig.defaultPassword (read on the same connection/transaction)
-- @PasswordUpdatedTime = timeProvider.GetUtcNow().UtcDateTime — the same clock that stamps the token's iat
INSERT INTO AppUser (UserId, UserName, IsActive, PasswordHash, PasswordUpdatedTime)
VALUES (@UserId, @UserName, @IsActive, @PasswordHash, @PasswordUpdatedTime);
SELECT CAST(SCOPE_IDENTITY() AS int);
-- then N-N sync
```

### SQL — UPDATE

```sql
UPDATE AppUser SET UserName = @UserName, IsActive = @IsActive WHERE UserId = @UserId;
-- then N-N sync; PasswordHash / PasswordUpdatedTime untouched
```

### SQL — RESET PASSWORD

```sql
-- @PasswordUpdatedTime = timeProvider.GetUtcNow().UtcDateTime (never GETUTCDATE(): it is the revocation stamp)
UPDATE AppUser SET PasswordHash = @PasswordHash, PasswordUpdatedTime = @PasswordUpdatedTime WHERE UserId = @UserId;
```

### SQL — DELETE

```sql
DELETE FROM AppUserRole WHERE UserId = @UserId;
DELETE FROM AppUser WHERE UserId = @UserId;
```

### RowAudit

| Action | `PrimaryKeyValues` | `ActionType` | `ActionDesc` |
|--------|--------------------|--------------|--------------|
| Create | `UserId` | `INSERT` | `UserName` |
| Update | `UserId` | `UPDATE` | changed columns (`UserName`, `IsActive`) + `RoleIds` when the set changed; **no row** when nothing changed |
| Reset password | `UserId` | `UPDATE` | `PasswordHash` (never "reset to default" — see *PasswordHash rules* 4) |
| Delete | `UserId` | `DELETE` | `UserName` |

Create / Update / Delete go through the generic `IRowAuditWriter.LogInsertAsync / LogUpdateAsync / LogDeleteAsync`
(row reloaded with its sorted `RoleIds` in the same transaction for the after-image); `UserId` is the model's
`[AuditKey]` and `RoleCount` is `[AuditIgnore]`d. Reset password keeps its custom `WriteAsync` description.

### Special Column Notes

- String PK: routes use `{id}`; the Angular service wraps ids in `encodeURIComponent`.
- `PasswordUpdatedTime` is `datetime` (Kind = Unspecified) → templates append `'Z'` before
  the date pipe.
- The SHA-256 **hex** format is an assumption (no existing hashes were available to inspect);
  change `PasswordHasher` if the legacy site used a different encoding.

---

## Frontend Notes

### Routes

| Route | Component |
|-------|-----------|
| `/admin/app-users` | `AppUserListComponent` |
| `/admin/app-users/new` | `AppUserFormComponent` |
| `/admin/app-users/:id` | `AppUserDetailComponent` (`id` = UserId) |
| `/admin/app-users/:id/edit` | `AppUserFormComponent` |

### Model (`core/models/app-user.model.ts`)

```ts
export interface AppUser { pkid: number; userId: string; userName: string; isActive: boolean;
  passwordUpdatedTime: string | null; roleCount: number; roleIds: string[]; }
export interface AppUserRequest { userId: string; userName: string; isActive: boolean; roleIds: string[]; }
export interface AppUserQuery { keyword?: string | null; isActive?: boolean | null; roleId?: string | null; }
```
No password field anywhere.

### Service (`core/services/app-user.service.ts`)

Six standard methods plus `resetPassword(userId)` → `POST …/app-users/{encoded id}/reset-password`.

### List page (`features/app-users/app-user-list/`)

Columns: 主代碼, 使用者代碼, 使用者名稱, 啟用 (tag 啟用／停用), 密碼更新時間, 角色數,
對應角色 (查看角色 button), 操作. Default sort `userId ASC`. Filter drawer: 關鍵字, 啟用
(全部／是／否), 角色 (`p-select` of roles). Incoming `?roleId=` overrides the saved filter.

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.userName}」？
```
plus `（將同時移除此使用者的 ${item.roleCount} 個角色）` when `roleCount > 0`.

### Session Storage Keys

`app-user-list-filters`, `app-user-list-sort`, `app-user-list-page`.

### Detail page (`features/app-users/app-user-detail/`)

Toolbar: 使用者 + `UserId UserName` + audit badge; actions 返回 / 編輯 / **重設密碼** / 刪除.
重設密碼 confirms, calls the reset endpoint, toasts 密碼已重設為系統預設密碼 and reloads so the
new 密碼更新時間 shows. Cards: 使用者資料 (主代碼, 使用者代碼, 使用者名稱, 啟用, 密碼更新時間),
角色 (tags with RoleName + 查看角色 button).

### Form page (`features/app-users/app-user-form/`)

| Field | Control | Validators / behaviour |
|-------|---------|------------------------|
| UserId 使用者代碼 | `pInputText` | required, maxLength 200; **disabled in edit mode** |
| UserName 使用者名稱 | `pInputText` | required, maxLength 200 |
| IsActive 啟用 | `p-checkbox binary` | default checked on new |
| RoleIds 角色 | `p-multiselect` (card 角色) | optional |

New mode shows the hint 新使用者的密碼將設為系統預設密碼. 409 → toast + `duplicate` error on
`userId`.

### Sidebar placement

Existing group 系統管理 Admin: insert `{ label: '使用者 AppUser', icon: 'pi pi-user',
routerLink: '/admin/app-users' }` right after 角色 AppRole.

---

## Tests

### Backend

- `Controllers\AppUsersControllerTests.cs` — GetAll, Query, GetById 200/404, Create 201 / 409 /
  500 (AppConfigException), Update 204/404, Delete 204/404/409, ResetPassword 204/404/500,
  request validation (UserId/UserName required and ≤ 200), and a reflection check that neither
  `AppUser` nor `AppUserRequest` exposes a `PasswordHash` member.
- `Infrastructure\PasswordHasherTests.cs` — SHA-256 of a known vector, lowercase hex, 64 chars.
- `Infrastructure\AppConfigJsonTests.cs` — extracts `defaultPassword`; missing row, invalid JSON,
  missing / empty property all throw `AppConfigException`.
- `Controllers\LookupsControllerTests.cs` — `app-users` now served by `IAppUserRepository`.

### Frontend

`app-user.service.spec.ts` (encoded ids, reset-password URL), list / detail / form specs
(no password control exists; reset flow; roleId override; disabled userId in edit), `app.spec.ts`.

---

## Files to Create / Modify

| Area | File | Action |
|------|------|--------|
| API | `Models\AppUser.cs`, `AppUserRequest.cs`, `AppUserQuery.cs` | Create |
| API | `Infrastructure\PasswordHasher.cs`, `AppConfigJson.cs`, `AppConfigException.cs` | Create |
| API | `Repositories\IAppUserRepository.cs`, `AppUserRepository.cs` | Create |
| API | `Controllers\AppUsersController.cs` | Create |
| API | `Repositories\ILookupRepository.cs`, `LookupRepository.cs` | Modify — drop app-users |
| API | `Controllers\LookupsController.cs`, `Program.cs` | Modify |
| Tests | `Controllers\AppUsersControllerTests.cs`, `Infrastructure\PasswordHasherTests.cs`, `Infrastructure\AppConfigJsonTests.cs` | Create |
| Tests | `Controllers\LookupsControllerTests.cs` | Modify |
| NG | `core\models\app-user.model.ts`, `core\services\app-user.service.ts` (+spec) | Create |
| NG | `features\app-users\app-user-list\*`, `app-user-detail\*`, `app-user-form\*` | Create |
| NG | `app.routes.ts`, `app.ts`, `app.spec.ts` | Modify |
| Docs | `CLAUDE.md` | Modify |

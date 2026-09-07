# Build Spec for AppRole
- database schema: `.\database\admin.sql`
- UI reference: `.\spec\ui-sample-list.png`, `ui-sample-view.png`, `ui-sample-edit.png`, `ui-sample-add.png` (these screenshots ARE the AppRole pages)

---

## Summary

`AppRole` is an application role used for authorisation. It has an identity `pkid` for
display, but the **clustered primary key is the string `RoleId`** — every route, FK and
audit key uses `RoleId`, not `pkid`. Roles are assigned to users through the junction
table `AppUserRole` (N-N with `AppUser`), which the role form manages with a multi-select.

| Item | Detail |
|------|--------|
| Primary Key | `RoleId` **nvarchar(200)** (string PK). `pkid` int IDENTITY exists but is not the key — shown as 主代碼 only. |
| Foreign Keys | None |
| Required Fields | `RoleId`, `RoleName`, `PermissionLevel` (DB default 100) |
| N-N Relationships | `AppUserRole` (`UserId`, `RoleId`) — AppRole ↔ AppUser |
| Primary-Foreign Links | N/A — `AppUserRole` is managed inline via the N-N section |
| Query Filters | keyword (`RoleId`, `RoleName`); `PermissionLevel` exact; `UserId` (roles held by a user) |
| Default Sort | `RoleId ASC` (matches the active sort column in `ui-sample-list.png`) |

---

## Localization

### Chinese Table Name

- AppRole: 角色
- Description: 使用者角色（權限群組）

### Chinese Column Names

- pkid: 主代碼
- RoleId: 角色代碼
- RoleName: 角色名稱
- PermissionLevel: 權限等級
- Description: 描述
- UserCount (computed in SELECT): 使用者數
- UserIds (N-N): 使用者

---

## Required Fields

Required (NOT NULL):
- `RoleId` — nvarchar(200), **user-supplied string key**, immutable after create
- `RoleName` — nvarchar(200)
- `PermissionLevel` — int, DB default `100` (`DF_AppRole_Privilege`); the form pre-fills 100 on new

Optional (nullable):
- `Description` — nvarchar(400). The sample screenshot marks it required, but the DB allows
  NULL; the DB wins — it is optional here.

---

## Foreign Keys

`AppRole` has no foreign key columns.

**N/A**

---

## Foreign-Primary Links

**N/A**

---

## Primary-Foreign Links

`AppUserRole` is the only table referencing `AppRole.RoleId`, and it is a junction table
handled by the N-N section below. No child-list navigation buttons.

**N/A**

---

## N-N Relationships

### AppRole ↔ AppUser via `AppUserRole`

| Column | Type | Notes |
|--------|------|-------|
| pkid | int IDENTITY | surrogate, unused |
| UserId | nvarchar(200) NOT NULL | FK → `AppUser.UserId` (composite PK part) |
| RoleId | nvarchar(200) NOT NULL | FK → `AppRole.RoleId` (composite PK part) |

- **List**: column 使用者數 = `(SELECT COUNT(*) FROM AppUserRole ur WHERE ur.RoleId = r.RoleId)`.
- **Detail**: show assigned users as chips/tags labelled `UserName (UserId)`; labels come
  from the user lookup loaded with `forkJoin`.
- **Form (edit + new)**: card 使用者 with `p-multiselect` (`[maxSelectedLabels]="9999"`,
  `[filter]="true"`, `appendTo="body"`, placeholder 選擇使用者（可複選）). Options from
  `GET /api/lookups/app-users`, label `UserName (UserId)`, ordered by `UserName`.
- **Request field**: `UserIds: List<string>` (`AppRoleRequest`); `AppRole.UserIds` populated
  on `GetByIdAsync` only (separate query, same connection).
- **Sync on save** (create and update, inside the same transaction):
  1. `DELETE FROM AppUserRole WHERE RoleId = @RoleId`
  2. `INSERT INTO AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId)` per distinct id
- **Delete**: junction rows are deleted first, then the role (so a role with users can be
  deleted; the confirm message shows the user count as a warning).

---

## Query Filters

- **keyword**: string?
  - LIKE on `RoleId`, `RoleName` (short identifying columns; `Description` excluded)

- **PermissionLevel**: int?
  - Exact match on `PermissionLevel`

- **UserId**: string?
  - `EXISTS (SELECT 1 FROM AppUserRole ur WHERE ur.RoleId = r.RoleId AND ur.UserId = @UserId)`
  - Dropdown from `GET /api/lookups/app-users`, `[filter]="true"`
  - Also accepted as incoming query param `userId` on the list route (for a future
    AppUser detail page "查看角色" button); overrides the saved filter state.

No bool or date-range filters (no bit/date columns).

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/app-users` | **New** | `StringLookupItem[]` — `{ id: UserId, label: UserName + ' (' + UserId + ')' }` ordered by `UserName` |
| `GET /api/lookups/app-roles` | **New** | `StringLookupItem[]` — `{ id: RoleId, label: RoleName }` ordered by `RoleId` (for the future AppUser form) |

`StringLookupItem` is a new shared DTO (`Id: string`, `Label: string`) because these keys
are strings; `LookupItem` (int pkid) stays for numeric keys. The user lookup lives in a new
`LookupRepository` so `LookupsController` does not depend on a not-yet-built AppUser
repository.

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/app-roles` | List all, `ORDER BY RoleId ASC`, includes `UserCount` |
| `POST` | `/api/app-roles/query` | Filtered query (body: `AppRoleQuery`) |
| `GET` | `/api/app-roles/{id}` | Get by **RoleId** (string, no `:int` constraint) → 200 (with `UserIds`) / 404 |
| `POST` | `/api/app-roles` | Create → 201 `CreatedAtAction(GetById, { id = RoleId })`; **409** if `RoleId` exists |
| `PUT` | `/api/app-roles` | Update (RoleId from body; RoleId immutable) → 204 / 404 |
| `DELETE` | `/api/app-roles/{id}` | Delete by RoleId (junction rows first) → 204 / 404 / 409 on unexpected FK |
| `GET` | `/api/lookups/app-users` | User lookup for the multi-select |
| `GET` | `/api/lookups/app-roles` | Role lookup |

Auth: none in this scaffold.

---

## Backend Notes

### Models

```csharp
public class AppRole
{
    public int Pkid { get; set; }
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public int PermissionLevel { get; set; }
    public string? Description { get; set; }
    /// Subquery count in list/query/get.
    public int UserCount { get; set; }
    /// Populated on GET by id only.
    public List<string> UserIds { get; set; } = [];
}

public class AppRoleRequest
{
    [Required(AllowEmptyStrings = false), StringLength(200)]
    public string RoleId { get; set; } = string.Empty;
    [Required(AllowEmptyStrings = false), StringLength(200)]
    public string RoleName { get; set; } = string.Empty;
    public int PermissionLevel { get; set; } = 100;
    [StringLength(400)]
    public string? Description { get; set; }
    public List<string> UserIds { get; set; } = [];
}

public class AppRoleQuery
{
    public string? Keyword { get; set; }
    public int? PermissionLevel { get; set; }
    public string? UserId { get; set; }
}

public class StringLookupItem
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
```

### SQL — SELECT

```sql
SELECT r.pkid, r.RoleId, r.RoleName, r.PermissionLevel, r.Description,
       (SELECT COUNT(*) FROM AppUserRole ur WHERE ur.RoleId = r.RoleId) AS UserCount
FROM AppRole r
-- QueryAsync WHERE:
--   (@Keyword IS NULL OR r.RoleId LIKE '%' + @Keyword + '%' OR r.RoleName LIKE '%' + @Keyword + '%')
--   AND (@PermissionLevel IS NULL OR r.PermissionLevel = @PermissionLevel)
--   AND (@UserId IS NULL OR EXISTS (SELECT 1 FROM AppUserRole ur WHERE ur.RoleId = r.RoleId AND ur.UserId = @UserId))
ORDER BY r.RoleId ASC
-- GetById additionally: SELECT UserId FROM AppUserRole WHERE RoleId = @RoleId ORDER BY UserId
```

### SQL — INSERT

```sql
INSERT INTO AppRole (RoleId, RoleName, PermissionLevel, Description)
VALUES (@RoleId, @RoleName, @PermissionLevel, @Description);
SELECT CAST(SCOPE_IDENTITY() AS int);
-- then N-N sync
```

### SQL — UPDATE

`RoleId` is immutable (it is the PK and the FK target of `AppUserRole`).

```sql
UPDATE AppRole
SET RoleName = @RoleName, PermissionLevel = @PermissionLevel, Description = @Description
WHERE RoleId = @RoleId;
-- then N-N sync
```

### SQL — DELETE

```sql
DELETE FROM AppUserRole WHERE RoleId = @RoleId;
DELETE FROM AppRole WHERE RoleId = @RoleId;
```

### N-N Sync Pattern

```sql
DELETE FROM AppUserRole WHERE RoleId = @RoleId;
INSERT INTO AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId);  -- per distinct UserId
```

### RowAudit

| Action | `TableName` | `PrimaryKeyValues` | `ActionType` | `ActionDesc` |
|--------|-------------|--------------------|--------------|--------------|
| Create | `AppRole` | `RoleId` | `INSERT` | `RoleName` |
| Update | `AppRole` | `RoleId` | `UPDATE` | changed scalar columns via `AuditHelper.ChangedColumns`, plus `UserIds` when the set changed |
| Delete | `AppRole` | `RoleId` | `DELETE` | `RoleName` |

### Special Column Notes

- **String PK**: controller routes use `{id}` with no `:int`; the Angular service wraps
  `getById`/`delete` ids in `encodeURIComponent`.
- `pkid` is IDENTITY and read-only; it is never in the request.
- `PermissionLevel` DB default 100 — mirrored in `AppRoleRequest` and the form default.
- No `nchar`, `date`, `time`, or computed-column handling required.

---

## Frontend Notes

### Routes

| Route | Component |
|-------|-----------|
| `/admin/app-roles` | `AppRoleListComponent` |
| `/admin/app-roles/new` | `AppRoleFormComponent` (new) |
| `/admin/app-roles/:id` | `AppRoleDetailComponent` (`id` = RoleId) |
| `/admin/app-roles/:id/edit` | `AppRoleFormComponent` (edit) |

### Model (`core/models/app-role.model.ts`)

```ts
export interface AppRole {
  pkid: number; roleId: string; roleName: string; permissionLevel: number;
  description: string | null; userCount: number; userIds: string[];
}
export interface AppRoleRequest {
  roleId: string; roleName: string; permissionLevel: number;
  description: string | null; userIds: string[];
}
export interface AppRoleQuery { keyword?: string | null; permissionLevel?: number | null; userId?: string | null; }
export interface StringLookupItem { id: string; label: string; }   // core/models/lookup-item.model.ts
```

### Service (`core/services/app-role.service.ts`)

Standard six methods against `${apiBaseUrl}/app-roles`; `getById(id)` and `delete(id)` use
`encodeURIComponent(id)`.

### List page (`features/app-roles/app-role-list/`)

- Header 角色 AppRole, subtitle 使用者角色; buttons 搜尋條件 (badge = active filter count)
  and 新增 — exactly as `ui-sample-list.png`.
- Columns: 主代碼, 角色代碼, 角色名稱, 權限等級, 描述, 使用者數, 操作 (view/edit/delete).
- Paginator on top, rows `[10, 20, 50]` default 20, default sort `roleId ASC`.
- Filter drawer: 關鍵字 (text), 權限等級 (`p-inputnumber`), 使用者 (`p-select` of users,
  `[filter]="true"`, `appendTo="body"`, 全部 option).
- Incoming `?userId=` query param overrides the saved `userId` filter.

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.roleName}」？
```
plus, when `userCount > 0`: `（將同時移除 ${item.userCount} 位使用者的此角色）`

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `app-role-list-filters` | Last `AppRoleQuery` |
| `app-role-list-sort` | `{ sortField, sortOrder }` |
| `app-role-list-page` | `{ first, rows }` |

### Lookup Binding in List

`forkJoin({ users: lookup.appUsers() })` on init populates the 使用者 filter dropdown.

### Detail page (`features/app-roles/app-role-detail/`)

- Sticky toolbar: title 角色 + `RoleId RoleName` + `RowAuditBadgeComponent`
  (`tableName="AppRole"`, `pk=roleId`); actions 返回 / 編輯 / 刪除.
- Card 角色資料: 主代碼, 角色代碼, 角色名稱, 權限等級, 描述.
- Card 使用者: tags `UserName (UserId)` (from user lookup), or 尚未指派使用者.

### Form page (`features/app-roles/app-role-form/`)

Mirrors `ui-sample-edit.png` / `ui-sample-add.png`:

| Field | Control | Validators / behaviour |
|-------|---------|------------------------|
| RoleId 角色代碼 | `pInputText` maxlength 200, placeholder 請輸入角色代碼 | `required`, `maxLength(200)`; **disabled in edit mode** |
| RoleName 角色名稱 | `pInputText` maxlength 200 | `required`, `maxLength(200)` |
| PermissionLevel 權限等級 | `p-inputnumber` `[useGrouping]=false` | `required`; default 100 on new |
| Description 描述 | `pInputText` maxlength 400 | optional, `maxLength(400)`; empty string → null |
| UserIds 使用者 | `p-multiselect` (card 使用者) | optional |

- `forkJoin({ users, item? })` on init; save uses `getRawValue()` so the disabled
  `roleId` is submitted; 409 → toast 角色代碼已存在 and `duplicate` error on the control.

### Sidebar placement

Existing entry 角色 AppRole → `/admin/app-roles` under 系統管理 Admin already exists in
`app.ts`; **no sidebar change**. Only routes are added.

---

## Tests

### Backend (`CMS.API.Tests`)

`Controllers\AppRolesControllerTests.cs` (Moq `IAppRoleRepository`): GetAll 200; Query
passes filter; GetById 200/404 with a string id; Create 201 at `GetById` with
`id = RoleId`, 409 on duplicate; Update 204/404; Delete 204/404/409; request validation
(`RoleId`/`RoleName` required, `RoleName` > 200 chars, `Description` > 400 chars).
`Controllers\LookupsControllerTests.cs`: add `app-users` and `app-roles` cases.

### Frontend (Karma + Jasmine)

- `app-role.service.spec.ts` — six methods; `getById('a/b c')` and `delete('a/b c')` hit
  `/app-roles/a%2Fb%20c`.
- `app-role-list.component.spec.ts` — renders rows, filter badge count, session keys,
  `userId` query param override, delete confirm text.
- `app-role-detail.component.spec.ts` — loads by RoleId from route, renders fields and
  user tags via lookup.
- `app-role-form.component.spec.ts` — new: invalid until roleId + roleName, default
  permissionLevel 100, create called with `userIds`; edit: roleId disabled, update called.

---

## Files to Create / Modify

| Area | File | Action |
|------|------|--------|
| API | `Models\AppRole.cs`, `AppRoleRequest.cs`, `AppRoleQuery.cs`, `StringLookupItem.cs` | Create |
| API | `Repositories\IAppRoleRepository.cs`, `AppRoleRepository.cs` | Create |
| API | `Repositories\ILookupRepository.cs`, `LookupRepository.cs` (app-users lookup) | Create |
| API | `Controllers\AppRolesController.cs` | Create |
| API | `Controllers\LookupsController.cs` | Modify — add `app-users`, `app-roles` |
| API | `Program.cs` | Modify — DI |
| Tests | `Controllers\AppRolesControllerTests.cs` | Create |
| Tests | `Controllers\LookupsControllerTests.cs` | Modify |
| NG | `core\models\app-role.model.ts` | Create |
| NG | `core\models\lookup-item.model.ts` | Modify — add `StringLookupItem` |
| NG | `core\services\app-role.service.ts` (+ spec) | Create |
| NG | `core\services\lookup.service.ts` | Modify — `appUsers()`, `appRoles()` |
| NG | `features\app-roles\app-role-list\*`, `app-role-detail\*`, `app-role-form\*` | Create |
| NG | `app.routes.ts` | Modify — lazy routes |

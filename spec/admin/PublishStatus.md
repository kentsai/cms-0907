# Build Spec for PublishStatus
- database schema: `.\database\admin.sql`

---

## Summary

`PublishStatus` is a small lookup table that classifies the publishing lifecycle of content
(course, promotion, …). Each row has a manually assigned `tinyint` key, a display
description, and three boolean flags that describe what the status means. It has no
foreign keys and no junction tables, but `Course` and `Promotion2` both reference it via
`PublishStatus_pkid`, so it must expose a slim lookup endpoint.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **tinyint NOT NULL — NOT IDENTITY**. The key is entered by the user on create and is immutable on update. |
| Foreign Keys | None |
| Required Fields | `pkid`, `Description`, `IsDraft`, `IsPublished`, `IsDiscontinued` |
| N-N Relationships | N/A |
| Primary-Foreign Links | `Course.PublishStatus_pkid`, `Promotion2.PublishStatus_pkid` |
| Query Filters | keyword (`Description`); tri-state bool on `IsDraft`, `IsPublished`, `IsDiscontinued` |
| Default Sort | `pkid ASC` |

---

## Localization

### Chinese Table Name

- PublishStatus: 發布狀態
- Description: 內容發布狀態代碼表（草稿／已發布／已下架）

### Chinese Column Names

- pkid: 主代碼
- Description: 狀態說明
- IsDraft: 草稿
- IsPublished: 已發布
- IsDiscontinued: 已下架

---

## Required Fields

Required (NOT NULL):
- `pkid` — tinyint, **user-supplied** (0–255), unique; read-only in edit mode
- `Description` — nvarchar(50)
- `IsDraft` — bit
- `IsPublished` — bit
- `IsDiscontinued` — bit

Optional (nullable):
- none

---

## Foreign Keys

`PublishStatus` has no foreign key columns.

**N/A**

---

## Foreign-Primary Links

`PublishStatus` has no foreign key columns.

**N/A**

---

## Primary-Foreign Links

The following tables reference `PublishStatus.pkid`. Show navigation buttons in the list
row and on the detail page. The target list pages do not exist yet in this repo; the
buttons navigate to the agreed route so they light up once those features are generated.

- **Course** (`PublishStatus_pkid`, `database\course.sql`)
  - Column header: 對應課程
  - Button label: 查看課程 (icon: `pi pi-book`)
  - Link to `/course/courses?publishStatusPkid={pkid}`
  - Course list accepts `publishStatusPkid` query param and sets `PublishStatusPkid` filter

- **Promotion2** (`PublishStatus_pkid`, `database\promotion.sql`)
  - Column header: 對應活動
  - Button label: 查看活動 (icon: `pi pi-megaphone`)
  - Link to `/promotion/promotions?publishStatusPkid={pkid}`
  - Promotion list accepts `publishStatusPkid` query param

---

## N-N Relationships

**N/A**

---

## Query Filters

- **keyword**: string?
  - LIKE on `Description` only (the sole string column)

- **IsDraft**: bool?
  - Exact match on `IsDraft`; tri-state (null = no filter)

- **IsPublished**: bool?
  - Exact match on `IsPublished`; tri-state

- **IsDiscontinued**: bool?
  - Exact match on `IsDiscontinued`; tri-state

No FK dropdown filters and no date-range filters (the table has no FK or date columns).

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/publish-statuses` | **New** — `LookupsController` does not exist yet; create it | `{ pkid, label }[]` ordered by `pkid ASC` |

This lookup is consumed by the future `Course` and `Promotion2` features (FK dropdown
`PublishStatus_pkid`, option label = `Description`, order by `pkid ASC`).

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/publish-statuses` | List all, `ORDER BY pkid ASC` |
| `POST` | `/api/publish-statuses/query` | Filtered query (body: `PublishStatusQuery`) |
| `GET` | `/api/publish-statuses/{id:int}` | Get by pkid → 200 / 404 |
| `POST` | `/api/publish-statuses` | Create. `pkid` comes from the body. → 201 `CreatedAtAction` with the entity; **409 Conflict** if `pkid` already exists — from the `ExistsAsync` pre-check, or from `DuplicateKeyException` (SQL 2627 / 2601) when a concurrent create wins the race between that read and the INSERT |
| `PUT` | `/api/publish-statuses` | Update (pkid from body) → 204 / 404 |
| `DELETE` | `/api/publish-statuses/{id:int}` | Delete → 204 / 404. Deleting a status still referenced by `Course`/`Promotion2` fails the FK constraint → **409 Conflict** with a message |
| `GET` | `/api/lookups/publish-statuses` | Slim lookup list (see above) |

Notes:
- `{id:int}` route constraint is used; the controller action parameter is `byte id`
  (a value above 255 fails model binding and returns 400, which is acceptable).
- Auth: a Bearer JWT is required on every action by the global `AuthorizeFilter`, and
  `PublishStatusesController` additionally carries `[Authorize(Policy = AuthorizationPolicies.Admin)]`
  — a signed-in non-administrator gets 403, mirrored in the SPA by `adminGuard` on
  `/admin/publish-statuses`. The `/api/lookups/publish-statuses` **lookup** is deliberately exempt and
  stays open to every signed-in user: it fills the course form's 上架狀態 dropdown.

---

## Backend Notes

### Models

```csharp
// Models/PublishStatus.cs
public class PublishStatus
{
    public byte Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDiscontinued { get; set; }
}

// Models/PublishStatusRequest.cs  — pkid is INCLUDED because it is not IDENTITY
public class PublishStatusRequest
{
    [Range(0, 255)]
    public byte Pkid { get; set; }

    [Required, StringLength(50)]
    public string Description { get; set; } = string.Empty;

    public bool IsDraft { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDiscontinued { get; set; }
}

// Models/PublishStatusQuery.cs
public class PublishStatusQuery
{
    public string? Keyword { get; set; }
    public bool? IsDraft { get; set; }
    public bool? IsPublished { get; set; }
    public bool? IsDiscontinued { get; set; }
}
```

### SQL — SELECT

Used by `GetAllAsync`, `QueryAsync`, `GetByIdAsync` (no JOINs, no aliases needed):

```sql
SELECT pkid, Description, IsDraft, IsPublished, IsDiscontinued
FROM PublishStatus
-- QueryAsync appends dynamic WHERE:
--   (@Keyword IS NULL OR Description LIKE '%' + @Keyword + '%')
--   AND (@IsDraft IS NULL OR IsDraft = @IsDraft)
--   AND (@IsPublished IS NULL OR IsPublished = @IsPublished)
--   AND (@IsDiscontinued IS NULL OR IsDiscontinued = @IsDiscontinued)
ORDER BY pkid ASC
```

### SQL — INSERT

`pkid` is written explicitly. There is no `SCOPE_IDENTITY()` — the repository returns
`request.Pkid`. Existence is checked first so a duplicate key yields 409 instead of a
SQL exception; the INSERT also catches SQL 2627 / 2601 (`SqlErrorNumbers.IsUniqueViolation`) and throws
`DuplicateKeyException` → the same 409, because that pre-check is a read followed by a write and a
concurrent create passes it.

```sql
INSERT INTO PublishStatus (pkid, Description, IsDraft, IsPublished, IsDiscontinued)
VALUES (@Pkid, @Description, @IsDraft, @IsPublished, @IsDiscontinued);
```

### SQL — UPDATE

`pkid` is immutable — used only in the WHERE clause.

```sql
UPDATE PublishStatus
SET Description = @Description,
    IsDraft = @IsDraft,
    IsPublished = @IsPublished,
    IsDiscontinued = @IsDiscontinued
WHERE pkid = @Pkid;
```

### SQL — DELETE

```sql
DELETE FROM PublishStatus WHERE pkid = @Pkid;
```

`SqlException` number **547** (FK violation) is caught in the repository and surfaced
as `EntityInUseException` → controller returns 409.

### N-N Sync Pattern

**N/A**

### RowAudit

`RowAudit` table lives in `admin.sql`. The scaffold has no writer yet, so this feature
introduces `Infrastructure\RowAuditWriter.cs` (registered as scoped) and the repository
calls it on the **same open connection**:

| Action | `TableName` | `PrimaryKeyValues` | `ActionType` | `ActionDesc` |
|--------|-------------|--------------------|--------------|--------------|
| Create | `PublishStatus` | `pkid` | `INSERT` | `Description` |
| Update | `PublishStatus` | `pkid` | `UPDATE` | comma-separated changed column names; no row when nothing changed |
| Delete | `PublishStatus` | `pkid` | `DELETE` | `Description` of the deleted row |

Now written through the generic `LogInsertAsync / LogUpdateAsync / LogDeleteAsync` (row reloaded after the change
for the after-image). `UserName` = the JWT `userName` claim, `"system"` when unauthenticated; `DateTime` =
`TimeProvider.GetUtcNow()` (UTC) — display with `+ 'Z'` on the frontend. See `spec\auth\Auth.md`.

A read endpoint `GET /api/row-audits?tableName=&pkid=` (new `RowAuditsController`) returns the
record's full audit trail newest-first (`DateTime`, `UserName`, `ActionType`, `ActionDesc`) so the
frontend 異動紀錄 History badge can show the latest entry inline and the whole trail in a dialog.

### Special Column Notes

- `pkid` is `tinyint` → C# `byte`; TypeScript `number`. Form uses `p-inputnumber`
  with `[min]="0" [max]="255"`, read-only (disabled) in edit mode.
- No `nchar`, no computed columns, no `date`/`time` columns → no Dapper type handlers
  needed.
- No default-value constraints on this table.

---

## Frontend Notes

### Routes

All lazy-loaded via `loadComponent`, registered in `app.routes.ts` (`/new` before `/:id`):

| Route | Component |
|-------|-----------|
| `/admin/publish-statuses` | `PublishStatusListComponent` |
| `/admin/publish-statuses/new` | `PublishStatusFormComponent` (new mode) |
| `/admin/publish-statuses/:id` | `PublishStatusDetailComponent` |
| `/admin/publish-statuses/:id/edit` | `PublishStatusFormComponent` (edit mode) |

### Model (`core/models/publish-status.model.ts`)

```ts
export interface PublishStatus {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
}
export type PublishStatusRequest = PublishStatus;
export interface PublishStatusQuery {
  keyword?: string | null;
  isDraft?: boolean | null;
  isPublished?: boolean | null;
  isDiscontinued?: boolean | null;
}
```

### Service (`core/services/publish-status.service.ts`)

`getAll()`, `query(q)`, `getById(id)`, `create(req)`, `update(req)`, `delete(id)` against
`${environment.apiBaseUrl}/publish-statuses`. Numeric PK → no `encodeURIComponent`.

### List page (`features/publish-statuses/publish-status-list/`)

- Header: **發布狀態 PublishStatus** with subtitle 內容發布狀態; toolbar buttons
  搜尋條件 (opens filter `p-drawer`) and 新增 (navigates to `/new`), matching
  `spec\ui-sample-list.png`.
- `p-table` columns (all sortable): 主代碼, 狀態說明, 草稿, 已發布, 已下架
  (bit columns rendered as `pi pi-check` / `pi pi-times` icons), 對應課程 / 對應活動
  link buttons, 操作 (view / edit / delete icon buttons).
- Paginator: rows-per-page `[10, 20, 50]`, default 20; `currentPageReportTemplate`
  `{first}–{last} 筆，共 {totalRecords} 筆`.
- Default sort `pkid ASC`.
- Filter drawer: `keyword` text input; three `p-select` tri-state controls
  (全部 / 是 / 否) for `isDraft`, `isPublished`, `isDiscontinued`; 查詢 and 清除 buttons.
  `p-select` has `appendTo="body"`.
- Delete uses `p-confirmdialog`; on success `MessageService` toast + reload.

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.description}」？
```

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `publish-status-list-filters` | Last `PublishStatusQuery` values |
| `publish-status-list-sort` | `{ sortField, sortOrder }` |
| `publish-status-list-page` | `{ first, rows }` |

No incoming cross-entity query params (nothing navigates *to* this list with a filter).

### Lookup Binding in List

No FK columns → no lookups to load. `forkJoin` is not needed on the list page.

### Detail page (`features/publish-statuses/publish-status-detail/`)

- Sticky `p-toolbar`: `#start` = title 發布狀態 + `RowAuditBadgeComponent`
  (`tableName="PublishStatus"`, `pk=pkid`); `#end` = 返回, 編輯, 刪除.
- Card 狀態資料 with a label/value grid for the five columns (bits as 是/否 tags).
- Card 相關資料 with Primary-Foreign link buttons 查看課程 / 查看活動.

### Form page (`features/publish-statuses/publish-status-form/`)

Reactive Forms. Layout mirrors `spec\ui-sample-edit.png` / `ui-sample-add.png`:
sticky `p-toolbar` (title 新增發布狀態 / 編輯發布狀態 + `RowAuditBadgeComponent` in
edit mode; 取消 / 儲存 on the end) above a card 狀態資料.

| Field | Control | Validators / behaviour |
|-------|---------|------------------------|
| pkid 主代碼 | `p-inputnumber` `[min]=0 [max]=255 [useGrouping]=false` | `required`, `min(0)`, `max(255)`; **disabled in edit mode** |
| Description 狀態說明 | `pInputText` `maxlength=50` | `required`, `maxLength(50)` |
| IsDraft 草稿 | `p-checkbox [binary]="true"` | default `false` |
| IsPublished 已發布 | `p-checkbox [binary]="true"` | default `false` |
| IsDiscontinued 已下架 | `p-checkbox [binary]="true"` | default `false` |

- Save button disabled while the form is invalid or a request is in flight.
- On 409 from create (duplicate pkid): show toast 主代碼已存在 and keep the form open.
- No lookups → `forkJoin` only wraps the `getById` call in edit mode.
- Edit mode uses `form.getRawValue()` so the disabled `pkid` control is still submitted.

### Date Handling

No date columns. `RowAuditBadgeComponent` appends `'Z'` before formatting audit
`dateTime` (Dapper returns `Kind = Unspecified`).

### Special Form Behaviors

- `pkid` is editable only on create. The three flags are independent checkboxes; no
  mutual-exclusion rule is enforced (the DB does not enforce one either).

### Sub-panels (edit mode only)

**N/A**

### Sidebar placement

Existing group **系統管理 Admin** in `app.ts` `menuItems`. Add after 角色 AppRole:

```ts
{ label: '發布狀態 PublishStatus', icon: 'pi pi-flag', routerLink: '/admin/publish-statuses' }
```

`app.html` needs no change for the menu (it is data-driven), but gains the shell-level
`<p-toast/>` and `<p-confirmdialog/>` used by every feature.

---

## Tests

### Backend (`CMS.API.Tests`)

`Controllers\PublishStatusesControllerTests.cs` — Moq `IPublishStatusRepository`:

- `GetAll` returns 200 with the repository list
- `Query` passes the `PublishStatusQuery` through and returns 200
- `GetById` → 200 when found, 404 when repository returns null
- `Create` → 201 `CreatedAtAction` pointing at `GetById` with the new pkid; 409 when
  the pkid already exists
- `Update` → 204 when the repository reports a row affected, 404 otherwise
- `Delete` → 204 / 404; 409 when the repository throws `EntityInUseException`
- Validation: `PublishStatusRequest` with empty `Description` or `Description` > 50
  chars fails `Validator.TryValidateObject` (documents the `[Required]`/`[StringLength]`
  rules that produce 400 through `[ApiController]`)

`Controllers\LookupsControllerTests.cs` — `publish-statuses` lookup returns 200 with
`{ pkid, label }` items.

### Frontend (Karma + Jasmine)

- `core/services/publish-status.service.spec.ts` — `provideHttpClientTesting()` +
  `HttpTestingController`; asserts URL and verb for all six methods.
- `publish-status-list.component.spec.ts` — mocked service; renders the table rows,
  restores/saves session-storage keys, opens the filter drawer.
- `publish-status-detail.component.spec.ts` — mocked service + `ActivatedRoute`
  (`id = 1`); renders the five fields and both link buttons.
- `publish-status-form.component.spec.ts` — new mode: form invalid until `pkid` and
  `description` are set; edit mode: `pkid` control is disabled and `getRawValue()`
  includes it; save calls `create` / `update` accordingly.
- Every component spec provides `provideRouter([])`, `provideNoopAnimations()`,
  `MessageService`, `ConfirmationService`.

---

## Files to Create / Modify

| Area | File | Action |
|------|------|--------|
| API | `src\CMS.API\Models\PublishStatus.cs` | Create |
| API | `src\CMS.API\Models\PublishStatusRequest.cs` | Create |
| API | `src\CMS.API\Models\PublishStatusQuery.cs` | Create |
| API | `src\CMS.API\Models\LookupItem.cs` | Create (shared `{ Pkid, Label }` DTO) |
| API | `src\CMS.API\Models\RowAudit.cs` | Create |
| API | `src\CMS.API\Infrastructure\RowAuditWriter.cs` | Create (shared audit writer) |
| API | `src\CMS.API\Infrastructure\EntityInUseException.cs` | Create (FK 547 → 409) |
| API | `src\CMS.API\Repositories\IPublishStatusRepository.cs` | Create |
| API | `src\CMS.API\Repositories\PublishStatusRepository.cs` | Create |
| API | `src\CMS.API\Repositories\IRowAuditRepository.cs` / `RowAuditRepository.cs` | Create (read side for badge) |
| API | `src\CMS.API\Controllers\PublishStatusesController.cs` | Create |
| API | `src\CMS.API\Controllers\LookupsController.cs` | Create |
| API | `src\CMS.API\Controllers\RowAuditsController.cs` | Create |
| API | `src\CMS.API\Program.cs` | Modify — DI registrations, `AddHttpContextAccessor` |
| Tests | `src\CMS.API.Tests\Controllers\PublishStatusesControllerTests.cs` | Create |
| Tests | `src\CMS.API.Tests\Controllers\LookupsControllerTests.cs` | Create |
| Tests | `src\CMS.API.Tests\UnitTest1.cs` | Delete (placeholder) |
| NG | `src\app\core\models\publish-status.model.ts` | Create |
| NG | `src\app\core\models\lookup-item.model.ts` | Create |
| NG | `src\app\core\models\row-audit.model.ts` | Create |
| NG | `src\app\core\services\publish-status.service.ts` (+ spec) | Create |
| NG | `src\app\core\services\lookup.service.ts` | Create |
| NG | `src\app\core\services\row-audit.service.ts` | Create |
| NG | `src\app\core\components\row-audit-badge\row-audit-badge.component.ts` | Create (shared) |
| NG | `src\app\features\publish-statuses\publish-status-list\*` (ts/html/scss/spec) | Create |
| NG | `src\app\features\publish-statuses\publish-status-detail\*` | Create |
| NG | `src\app\features\publish-statuses\publish-status-form\*` | Create |
| NG | `src\app\app.routes.ts` | Modify — lazy routes |
| NG | `src\app\app.ts` | Modify — sidebar entry under 系統管理 Admin |
| NG | `src\app\app.config.ts` | Modify — provide `MessageService`, `ConfirmationService` |
| NG | `src\app\app.html` | Modify — add `<p-toast/>` and `<p-confirmdialog/>` once at shell level |

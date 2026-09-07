# Build Spec for CourseGroup
- database schema: `.\database\course.sql`

---

## Summary

`CourseGroup` is a small classification table: a named bucket that courses are filed under
(e.g. 雲端、資安、專案管理). Each row has only a server-assigned `pkid` and a `Description`.
It has **no foreign keys**, but two tables point at it — `Course.CourseGroup_pkid`
(nullable) and `PartnerCourseGroup.CourseGroup_pkid` (required) — so it must expose a slim
lookup endpoint for those forms.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **smallint IDENTITY(1,1)** → C# `short`; server-assigned, omitted on create, immutable on update |
| Foreign Keys | None |
| Required Fields | `Description` |
| N-N Relationships | N/A — `PartnerCourseGroup` links Partner ↔ CourseGroup but carries `DisplayOrder` + its own `Description`, so it is a child entity, not a pure junction (see Primary-Foreign Links) |
| Primary-Foreign Links | `Course.CourseGroup_pkid` (nullable), `PartnerCourseGroup.CourseGroup_pkid` |
| Query Filters | keyword (`Description`) |
| Default Sort | `pkid DESC` (no `DisplayOrder` column; the lookup is ordered by `Description ASC`) |

---

## Localization

### Chinese Table Name

- CourseGroup: 課程群組
- Description: 課程分類群組主檔（僅有群組說明），供課程與夥伴課程群組引用

### Chinese Column Names

- pkid: 主代碼
- Description: 說明

---

## Required Fields

Required (NOT NULL):
- `Description` — nvarchar(100)

Optional (nullable):
- none

---

## Foreign Keys

`CourseGroup` has no foreign key columns.

**N/A**

---

## Foreign-Primary Links

`CourseGroup` has no foreign key columns.

**N/A**

---

## Primary-Foreign Links

Two tables reference `CourseGroup.pkid`. Neither target list page exists yet in this repo;
the buttons navigate to the agreed route so they light up once those features are
generated. Both links are shown on the **list** and the **detail** page (only two, so the
row stays narrow).

- **Course** (`CourseGroup_pkid`, nullable, `database\course.sql`)
  - Column header: 對應課程
  - Button label: 查看課程 (icon: `pi pi-book`)
  - Link to `/course/courses?courseGroupPkid={pkid}`
  - Course list accepts `courseGroupPkid` query param and sets `CourseGroupPkid` filter
  - Shown in: list + detail

- **PartnerCourseGroup** (`CourseGroup_pkid`, `database\course.sql`)
  - Column header: 對應夥伴課程群組
  - Button label: 查看夥伴課程群組 (icon: `pi pi-sitemap`)
  - Link to `/course/partner-course-groups?courseGroupPkid={pkid}`
  - PartnerCourseGroup list accepts `courseGroupPkid` query param
  - Shown in: list + detail

---

## N-N Relationships

**N/A** — `PartnerCourseGroup(pkid, Partner_pkid, CourseGroup_pkid, DisplayOrder, Description)`
has two FKs but also payload columns and an identity PK, so it is modelled as a child entity
with its own CRUD (future feature), not as a multiselect on the CourseGroup form.

---

## Query Filters

- **keyword**: string?
  - LIKE on `Description` (the only string column)

No FK dropdown filters, no bool filters and no date-range filters (the table has no FK,
bit or date columns).

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/course-groups` | **New** — add action to existing `LookupsController` | `{ pkid, label }[]`, label = `Description`, ordered by `Description ASC, pkid ASC` |

This lookup is consumed by the future `Course` (nullable FK → dropdown gets a 無 option)
and `PartnerCourseGroup` features.

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/course-groups` | List all, `ORDER BY pkid DESC` |
| `POST` | `/api/course-groups/query` | Filtered query (body: `CourseGroupQuery`) |
| `GET` | `/api/course-groups/{id:int}` | Get by pkid → 200 / 404 |
| `POST` | `/api/course-groups` | Create. `pkid` in the body is ignored; the new identity is returned → 201 `CreatedAtAction` with the entity |
| `PUT` | `/api/course-groups` | Update (pkid from body) → 204 / 404 |
| `DELETE` | `/api/course-groups/{id:int}` | Delete → 204 / 404. Deleting a group still referenced by Course or PartnerCourseGroup fails the FK constraint → **409 Conflict** with a message |
| `GET` | `/api/lookups/course-groups` | Slim lookup list (see above) |

Notes:
- `{id:int}` route constraint; the controller action parameter is `short id`
  (a value outside the smallint range fails model binding → 400, acceptable).
- Auth: none in this scaffold. No `[Authorize]` attributes.

---

## Backend Notes

### Models

```csharp
// Models/CourseGroup.cs
public class CourseGroup
{
    public short Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
}

// Models/CourseGroupRequest.cs — Pkid is present because PUT takes it from the body;
// it is ignored on POST (IDENTITY).
public class CourseGroupRequest
{
    public short Pkid { get; set; }

    [Required(AllowEmptyStrings = false), StringLength(100)]
    public string Description { get; set; } = string.Empty;
}

// Models/CourseGroupQuery.cs
public class CourseGroupQuery
{
    public string? Keyword { get; set; }
}
```

### SQL — SELECT

Used by `GetAllAsync`, `QueryAsync`, `GetByIdAsync` (no JOINs, no aliases needed):

```sql
SELECT pkid, Description
FROM CourseGroup
-- QueryAsync appends dynamic WHERE:
--   (@Keyword IS NULL OR Description LIKE '%' + @Keyword + '%')
ORDER BY pkid DESC
```

### SQL — INSERT

`pkid` is IDENTITY and excluded. The new key is returned as `short`.

```sql
INSERT INTO CourseGroup (Description)
VALUES (@Description);
SELECT CAST(SCOPE_IDENTITY() AS smallint);
```

### SQL — UPDATE

`pkid` is immutable — used only in the WHERE clause.

```sql
UPDATE CourseGroup
SET Description = @Description
WHERE pkid = @Pkid;
```

### SQL — DELETE

```sql
DELETE FROM CourseGroup WHERE pkid = @Pkid;
```

`SqlException` number **547** (FK violation from Course / PartnerCourseGroup) is caught in
the repository and surfaced as `EntityInUseException` → controller returns 409.

### N-N Sync Pattern

**N/A**

### RowAudit

Uses the existing `IRowAuditWriter` on the **same open connection/transaction**:

| Action | `TableName` | `PrimaryKeyValues` | `ActionType` | `ActionDesc` |
|--------|-------------|--------------------|--------------|--------------|
| Create | `CourseGroup` | new `pkid` | `INSERT` | `Description` |
| Update | `CourseGroup` | `pkid` | `UPDATE` | comma-separated changed column names (`AuditHelper.ChangedColumns`) |
| Delete | `CourseGroup` | `pkid` | `DELETE` | `Description` of the deleted row |

### Special Column Notes

- `pkid` is `smallint IDENTITY` → C# `short`; TypeScript `number`. Not shown on the new
  form; read-only text on the edit form.
- `Description` is `nvarchar` → full Unicode, trimmed before save.
- No `nchar`, `varchar`, computed, `date`/`time` columns → no ASCII validation and no
  Dapper type handlers needed.
- No default-value constraints on this table.

---

## Frontend Notes

### Routes

All lazy-loaded via `loadComponent`, registered in `app.routes.ts` (`/new` before `/:id`):

| Route | Component |
|-------|-----------|
| `/course/course-groups` | `CourseGroupListComponent` |
| `/course/course-groups/new` | `CourseGroupFormComponent` (new mode) |
| `/course/course-groups/:id` | `CourseGroupDetailComponent` |
| `/course/course-groups/:id/edit` | `CourseGroupFormComponent` (edit mode) |

### Model (`core/models/course-group.model.ts`)

```ts
export interface CourseGroup {
  pkid: number;
  description: string;
}
/** pkid is ignored on create (send 0) and identifies the row on update. */
export type CourseGroupRequest = CourseGroup;
export interface CourseGroupQuery {
  keyword?: string | null;
}
```

### Service (`core/services/course-group.service.ts`)

`getAll()`, `query(q)`, `getById(id)`, `create(req)`, `update(req)`, `delete(id)` against
`${environment.apiBaseUrl}/course-groups`. Numeric PK → no `encodeURIComponent`.

### List page (`features/course-groups/course-group-list/`)

- Header: **課程群組 CourseGroup** with subtitle 課程分類群組; toolbar buttons
  搜尋條件 (opens filter `p-drawer`, badge = active filter count) and 新增.
- `p-table` columns (sortable unless noted): 主代碼, 說明, 對應課程 / 對應夥伴課程群組
  link buttons (not sortable), 操作 (view / edit / delete icon buttons).
- Paginator: rows-per-page `[10, 20, 50]`, default 20; `currentPageReportTemplate`
  `{first}–{last} 筆，共 {totalRecords} 筆`.
- Default sort `pkid DESC` (matches the API order).
- Filter drawer: `keyword` text input only; 查詢 and 清除 buttons.
- Delete uses `p-confirmdialog`; on success `MessageService` toast + reload; 409 shows the
  API message.

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.description}」？
```

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `course-group-list-filters` | Last `CourseGroupQuery` values |
| `course-group-list-sort` | `{ sortField, sortOrder }` |
| `course-group-list-page` | `{ first, rows }` |

No incoming cross-entity query params (nothing navigates *to* this list with a filter).

### Lookup Binding in List

No FK columns → no lookups to load. `forkJoin` is not needed on the list page.

### Detail page (`features/course-groups/course-group-detail/`)

- Sticky `p-toolbar`: `#start` = title 課程群組 + `pkid　Description` subtitle +
  `RowAuditBadgeComponent` (`tableName="CourseGroup"`, `pk=pkid`); `#end` = 返回, 編輯, 刪除.
- Card 群組資料 with a label/value grid for the two columns.
- Card 相關資料 with both Primary-Foreign link buttons.

### Form page (`features/course-groups/course-group-form/`)

Reactive Forms. Sticky `p-toolbar` (title 新增課程群組 / 編輯課程群組 +
`RowAuditBadgeComponent` in edit mode; 取消 / 儲存 on the end) above a card 群組資料.

| Field | Control | Validators / behaviour |
|-------|---------|------------------------|
| pkid 主代碼 | read-only text (edit mode only) | not a form control; shown as a hint |
| Description 說明 | `pInputText` `maxlength=100` | `required`, `maxLength(100)` |

- Save button disabled while the form is invalid or a request is in flight.
- String values are trimmed before submit.
- No lookups → `forkJoin` only wraps the `getById` call in edit mode.
- On save success navigate to `/course/course-groups/{pkid}` (create uses the pkid returned
  in the 201 body).

### Date Handling

No date columns. `RowAuditBadgeComponent` appends `'Z'` before formatting audit
`dateTime` (Dapper returns `Kind = Unspecified`).

### Special Form Behaviors

- `pkid` is never editable (IDENTITY). New mode sends `pkid: 0`.
- No cross-field defaults.

### Sub-panels (edit mode only)

**N/A** (a future `PartnerCourseGroup` feature may add an inline sub-panel here).

### Sidebar placement

**Existing group 課程管理 Course** in `app.ts` `menuItems`, appended after 合作夥伴 Partner:

```ts
{ label: '課程群組 CourseGroup', icon: 'pi pi-folder', routerLink: '/course/course-groups' }
```

`app.html` needs no change (the menu is data-driven).

---

## Tests

### Backend (`CMS.API.Tests`)

`Controllers\CourseGroupsControllerTests.cs` — Moq `ICourseGroupRepository` (strict):

- `GetAll` returns 200 with the repository list
- `Query` passes the `CourseGroupQuery` through and returns 200
- `GetById` → 200 when found, 404 when repository returns null
- `Create` → 201 `CreatedAtAction` pointing at `GetById` with the new pkid from the
  repository; body echoes the request with the assigned pkid
- `Update` → 204 when the repository reports a row affected, 404 otherwise
- `Delete` → 204 / 404; 409 when the repository throws `EntityInUseException`
- Validation: `CourseGroupRequest` with blank `Description` or `Description` > 100 chars
  fails `Validator.TryValidateObject`; a 100-char value is valid.

`Controllers\LookupsControllerTests.cs` — add `CourseGroups_ReturnsOk_WithLookupItems`
(constructor gains an `ICourseGroupRepository` mock).

### Frontend (Karma + Jasmine)

- `core/services/course-group.service.spec.ts` — `provideHttpClientTesting()` +
  `HttpTestingController`; asserts URL and verb for all six methods.
- `course-group-list.component.spec.ts` — mocked service; renders the table rows, link
  buttons, filter badge, restores/saves session-storage keys, confirms delete with pkid +
  description.
- `course-group-detail.component.spec.ts` — mocked service + `ActivatedRoute` (`id = 1`);
  renders the two fields, both link buttons, the row-audit badge, and the 404 state.
- `course-group-form.component.spec.ts` — new mode: form invalid until `Description` is
  set; over-long description rejected; value trimmed on submit; edit mode: loads by id,
  submits with the original pkid, renders the edit title and audit badge.
- `app.spec.ts` — add an assertion that the sidebar lists 課程群組 CourseGroup.
- Every component spec provides `provideRouter([])`, `provideNoopAnimations()`,
  `MessageService`, `ConfirmationService`.

---

## Files to Create / Modify

| Area | File | Action |
|------|------|--------|
| API | `src\CMS.API\Models\CourseGroup.cs` | Create |
| API | `src\CMS.API\Models\CourseGroupRequest.cs` | Create |
| API | `src\CMS.API\Models\CourseGroupQuery.cs` | Create |
| API | `src\CMS.API\Repositories\ICourseGroupRepository.cs` | Create |
| API | `src\CMS.API\Repositories\CourseGroupRepository.cs` | Create |
| API | `src\CMS.API\Controllers\CourseGroupsController.cs` | Create |
| API | `src\CMS.API\Controllers\LookupsController.cs` | Modify — add `course-groups` action |
| API | `src\CMS.API\Program.cs` | Modify — DI registration |
| Tests | `src\CMS.API.Tests\Controllers\CourseGroupsControllerTests.cs` | Create |
| Tests | `src\CMS.API.Tests\Controllers\LookupsControllerTests.cs` | Modify — course-groups lookup test |
| NG | `src\app\core\models\course-group.model.ts` | Create |
| NG | `src\app\core\services\course-group.service.ts` (+ spec) | Create |
| NG | `src\app\core\services\lookup.service.ts` | Modify — `courseGroups()` |
| NG | `src\app\features\course-groups\course-group-list\*` (ts/html/scss/spec) | Create |
| NG | `src\app\features\course-groups\course-group-detail\*` | Create |
| NG | `src\app\features\course-groups\course-group-form\*` | Create |
| NG | `src\app\app.routes.ts` | Modify — lazy routes under `course/course-groups` |
| NG | `src\app\app.ts` | Modify — sidebar entry under 課程管理 Course |
| NG | `src\app\app.spec.ts` | Modify — sidebar assertion |
| Docs | `CLAUDE.md` | Modify — menu + status |

# Build Spec for Partner
- database schema: `.\database\course.sql`

---

## Summary

`Partner` is the master list of training partners / vendors (e.g. Microsoft, Cisco, PMI)
whose courses the site sells. Each row carries the partner's canonical name, a short
`AppKey` used by the public site, two alternative display names (one for the partner menu,
one for the course detail page), a manual display order, and an optional logo filename. It
has **no foreign keys**, but it is one of the most referenced tables in the schema: `Course`,
`Certification`, `PartnerCourseGroup` (course.sql), `Seminar` and `Promotion2`
(promotion.sql) all point at `Partner.pkid`, so it must expose a slim lookup endpoint.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **smallint IDENTITY(1,1)** → C# `short`; server-assigned, omitted on create, immutable on update |
| Foreign Keys | None |
| Required Fields | `Name`, `AppKey`, `NameOnPartnerMenu`, `NameOnCourseDetailPage`, `DisplayOrder` |
| N-N Relationships | N/A — `PartnerCourseGroup` links Partner ↔ CourseGroup but carries its own `DisplayOrder` + `Description`, so it is a child entity, not a pure junction (see Primary-Foreign Links) |
| Primary-Foreign Links | `Course.Partner_pkid`, `Certification.Partner_pkid`, `PartnerCourseGroup.Partner_pkid`, `Seminar.Partner_pkid`, `Promotion2.RelatedPartner_pkid` |
| Query Filters | keyword (`Name`, `AppKey`, `NameOnPartnerMenu`, `NameOnCourseDetailPage`) |
| Default Sort | `DisplayOrder ASC, pkid DESC` |

---

## Localization

### Chinese Table Name

- Partner: 合作夥伴
- Description: 課程合作夥伴／原廠主檔（名稱、選單顯示名稱、顯示順序、Logo 圖檔）

### Chinese Column Names

- pkid: 主代碼
- Name: 名稱
- AppKey: 應用程式代碼
- NameOnPartnerMenu: 夥伴選單名稱
- NameOnCourseDetailPage: 課程頁顯示名稱
- DisplayOrder: 顯示順序
- ImageFilename: 圖片檔名

---

## Required Fields

Required (NOT NULL):
- `Name` — nvarchar(50)
- `AppKey` — varchar(10). **ASCII only** (the column is `varchar`; non-ASCII input would be
  stored as `?`). Validated with a printable-ASCII, no-whitespace regex on both sides.
- `NameOnPartnerMenu` — nvarchar(200)
- `NameOnCourseDetailPage` — nvarchar(50)
- `DisplayOrder` — int

Optional (nullable):
- `ImageFilename` — varchar(50). Same ASCII-only rule as `AppKey` (allows `.`, `-`, `_`).
  Empty string is normalised to `null`.

---

## Foreign Keys

`Partner` has no foreign key columns.

**N/A**

---

## Foreign-Primary Links

`Partner` has no foreign key columns.

**N/A**

---

## Primary-Foreign Links

The following tables reference `Partner.pkid`. None of the target list pages exist yet in
this repo; the buttons navigate to the agreed route so they light up once those features
are generated. The **list page** shows only the two most useful links (課程, 認證) to keep
the row narrow; the **detail page** shows all five.

- **Course** (`Partner_pkid`, `database\course.sql`)
  - Column header: 對應課程
  - Button label: 查看課程 (icon: `pi pi-book`)
  - Link to `/course/courses?partnerPkid={pkid}`
  - Course list accepts `partnerPkid` query param and sets `PartnerPkid` filter
  - Shown in: list + detail

- **Certification** (`Partner_pkid`, `database\course.sql`)
  - Column header: 對應認證
  - Button label: 查看認證 (icon: `pi pi-verified`)
  - Link to `/course/certifications?partnerPkid={pkid}`
  - Shown in: list + detail

- **PartnerCourseGroup** (`Partner_pkid`, `database\course.sql`)
  - Button label: 查看課程群組 (icon: `pi pi-sitemap`)
  - Link to `/course/partner-course-groups?partnerPkid={pkid}`
  - Shown in: detail only

- **Seminar** (`Partner_pkid`, nullable, `database\promotion.sql`)
  - Button label: 查看說明會 (icon: `pi pi-calendar`)
  - Link to `/promotion/seminars?partnerPkid={pkid}`
  - Shown in: detail only

- **Promotion2** (`RelatedPartner_pkid`, nullable, `database\promotion.sql`)
  - Button label: 查看活動 (icon: `pi pi-megaphone`)
  - Link to `/promotion/promotions?relatedPartnerPkid={pkid}`
  - Note the param name is `relatedPartnerPkid`, matching the column name on `Promotion2`.
  - Shown in: detail only

---

## N-N Relationships

**N/A** — `PartnerCourseGroup(pkid, Partner_pkid, CourseGroup_pkid, DisplayOrder, Description)`
has two FKs but also its own payload columns and identity PK, so it is modelled as a child
entity with its own CRUD (future feature), not as a multiselect on the Partner form.

---

## Query Filters

- **keyword**: string?
  - LIKE on `Name`, `AppKey`, `NameOnPartnerMenu`, `NameOnCourseDetailPage`
    (all four short identifying strings; `ImageFilename` is excluded — it is not a name)

No FK dropdown filters, no bool filters and no date-range filters (the table has no FK,
bit or date columns).

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/partners` | **New** — add action to existing `LookupsController` | `{ pkid, label }[]`, label = `Name`, ordered by `DisplayOrder ASC, Name ASC` |

This lookup is consumed by the future `Course`, `Certification`, `PartnerCourseGroup`,
`Seminar` and `Promotion2` features (FK dropdown `Partner_pkid` / `RelatedPartner_pkid`).

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/partners` | List all, `ORDER BY DisplayOrder ASC, pkid DESC` |
| `POST` | `/api/partners/query` | Filtered query (body: `PartnerQuery`) |
| `GET` | `/api/partners/{id:int}` | Get by pkid → 200 / 404 |
| `POST` | `/api/partners` | Create. `pkid` in the body is ignored; the new identity is returned → 201 `CreatedAtAction` with the entity |
| `PUT` | `/api/partners` | Update (pkid from body) → 204 / 404 |
| `DELETE` | `/api/partners/{id:int}` | Delete → 204 / 404. Deleting a partner still referenced by any child table fails the FK constraint → **409 Conflict** with a message |
| `GET` | `/api/lookups/partners` | Slim lookup list (see above) |

Notes:
- `{id:int}` route constraint; the controller action parameter is `short id`
  (a value outside the smallint range fails model binding → 400, acceptable).
- Auth: none in this scaffold. No `[Authorize]` attributes.

---

## Backend Notes

### Models

```csharp
// Models/Partner.cs
public class Partner
{
    public short Pkid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public string NameOnPartnerMenu { get; set; } = string.Empty;
    public string NameOnCourseDetailPage { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string? ImageFilename { get; set; }
}

// Models/PartnerRequest.cs — Pkid is present because PUT takes it from the body;
// it is ignored on POST (IDENTITY).
public class PartnerRequest
{
    public short Pkid { get; set; }

    [Required(AllowEmptyStrings = false), StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false), StringLength(10)]
    [RegularExpression(@"^[\x21-\x7E]+$")]           // printable ASCII, no whitespace (varchar column)
    public string AppKey { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false), StringLength(200)]
    public string NameOnPartnerMenu { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false), StringLength(50)]
    public string NameOnCourseDetailPage { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    [StringLength(50)]
    [RegularExpression(@"^[\x21-\x7E]+$")]           // varchar column
    public string? ImageFilename { get; set; }
}

// Models/PartnerQuery.cs
public class PartnerQuery
{
    public string? Keyword { get; set; }
}
```

### SQL — SELECT

Used by `GetAllAsync`, `QueryAsync`, `GetByIdAsync` (no JOINs, no aliases needed):

```sql
SELECT pkid, Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage, DisplayOrder, ImageFilename
FROM Partner
-- QueryAsync appends dynamic WHERE:
--   (@Keyword IS NULL
--      OR Name LIKE '%' + @Keyword + '%'
--      OR AppKey LIKE '%' + @Keyword + '%'
--      OR NameOnPartnerMenu LIKE '%' + @Keyword + '%'
--      OR NameOnCourseDetailPage LIKE '%' + @Keyword + '%')
ORDER BY DisplayOrder ASC, pkid DESC
```

### SQL — INSERT

`pkid` is IDENTITY and excluded. The new key is returned as `short`.

```sql
INSERT INTO Partner (Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage, DisplayOrder, ImageFilename)
VALUES (@Name, @AppKey, @NameOnPartnerMenu, @NameOnCourseDetailPage, @DisplayOrder, @ImageFilename);
SELECT CAST(SCOPE_IDENTITY() AS smallint);
```

### SQL — UPDATE

`pkid` is immutable — used only in the WHERE clause.

```sql
UPDATE Partner
SET Name = @Name,
    AppKey = @AppKey,
    NameOnPartnerMenu = @NameOnPartnerMenu,
    NameOnCourseDetailPage = @NameOnCourseDetailPage,
    DisplayOrder = @DisplayOrder,
    ImageFilename = @ImageFilename
WHERE pkid = @Pkid;
```

### SQL — DELETE

```sql
DELETE FROM Partner WHERE pkid = @Pkid;
```

`SqlException` number **547** (FK violation from Course / Certification /
PartnerCourseGroup / Seminar / Promotion2) is caught in the repository and surfaced as
`EntityInUseException` → controller returns 409.

### N-N Sync Pattern

**N/A**

### RowAudit

Uses `IRowAuditWriter.LogInsertAsync / LogUpdateAsync / LogDeleteAsync` on the **same open
connection/transaction** (the row is reloaded after the change for the after-image; covered by
`Tests\Repositories\PartnerRepositoryTests`):

| Action | `TableName` | `PrimaryKeyValues` | `ActionType` | `ActionDesc` |
|--------|-------------|--------------------|--------------|--------------|
| Create | `Partner` | new `pkid` | `INSERT` | `Name` |
| Update | `Partner` | `pkid` | `UPDATE` | comma-separated changed column names; no row when nothing changed |
| Delete | `Partner` | `pkid` | `DELETE` | `Name` of the deleted row |

### Special Column Notes

- `pkid` is `smallint IDENTITY` → C# `short`; TypeScript `number`. Not shown on the new
  form; read-only text on the edit form.
- `AppKey` and `ImageFilename` are `varchar` (not `nvarchar`) → ASCII-only validation on
  both sides; `ImageFilename` empty string is normalised to `null` before saving.
- No `nchar`, no computed columns, no `date`/`time` columns → no Dapper type handlers
  needed.
- No default-value constraints on this table.

---

## Frontend Notes

### Routes

All lazy-loaded via `loadComponent`, registered in `app.routes.ts` (`/new` before `/:id`):

| Route | Component |
|-------|-----------|
| `/course/partners` | `PartnerListComponent` |
| `/course/partners/new` | `PartnerFormComponent` (new mode) |
| `/course/partners/:id` | `PartnerDetailComponent` |
| `/course/partners/:id/edit` | `PartnerFormComponent` (edit mode) |

### Model (`core/models/partner.model.ts`)

```ts
export interface Partner {
  pkid: number;
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename: string | null;
}
/** pkid is ignored on create (send 0) and identifies the row on update. */
export type PartnerRequest = Partner;
export interface PartnerQuery {
  keyword?: string | null;
}
```

### Service (`core/services/partner.service.ts`)

`getAll()`, `query(q)`, `getById(id)`, `create(req)`, `update(req)`, `delete(id)` against
`${environment.apiBaseUrl}/partners`. Numeric PK → no `encodeURIComponent`.

### List page (`features/partners/partner-list/`)

- Header: **合作夥伴 Partner** with subtitle 課程合作夥伴／原廠; toolbar buttons
  搜尋條件 (opens filter `p-drawer`, badge = active filter count) and 新增.
- `p-table` columns (sortable unless noted): 主代碼, 名稱, 應用程式代碼, 夥伴選單名稱,
  課程頁顯示名稱, 顯示順序, 圖片檔名, 對應課程 / 對應認證 link buttons (not sortable),
  操作 (view / edit / delete icon buttons).
- Paginator: rows-per-page `[10, 20, 50]`, default 20; `currentPageReportTemplate`
  `{first}–{last} 筆，共 {totalRecords} 筆`.
- Default sort `displayOrder ASC` (client-side secondary order follows API order
  `pkid DESC`).
- Filter drawer: `keyword` text input only; 查詢 and 清除 buttons.
- Delete uses `p-confirmdialog`; on success `MessageService` toast + reload; 409 shows the
  API message.

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.name}」？
```

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `partner-list-filters` | Last `PartnerQuery` values |
| `partner-list-sort` | `{ sortField, sortOrder }` |
| `partner-list-page` | `{ first, rows }` |

No incoming cross-entity query params (nothing navigates *to* this list with a filter).

### Lookup Binding in List

No FK columns → no lookups to load. `forkJoin` is not needed on the list page.

### Detail page (`features/partners/partner-detail/`)

- Sticky `p-toolbar`: `#start` = title 合作夥伴 + `pkid　Name` subtitle +
  `RowAuditBadgeComponent` (`tableName="Partner"`, `[pkid]="pkid"`); `#end` = 返回, 編輯, 刪除.
- Card 夥伴資料 with a label/value grid for the seven columns (`ImageFilename` shows
  `—` when null).
- Card 相關資料 with all five Primary-Foreign link buttons.

### Form page (`features/partners/partner-form/`)

Reactive Forms. Sticky `p-toolbar` (title 新增合作夥伴 / 編輯合作夥伴 +
`RowAuditBadgeComponent` in edit mode; 取消 / 儲存 on the end) above a card 夥伴資料.

| Field | Control | Validators / behaviour |
|-------|---------|------------------------|
| pkid 主代碼 | read-only text (edit mode only) | not a form control; shown as a hint |
| Name 名稱 | `pInputText` `maxlength=50` | `required`, `maxLength(50)` |
| AppKey 應用程式代碼 | `pInputText` `maxlength=10` | `required`, `maxLength(10)`, `pattern(/^[\x21-\x7E]+$/)` |
| NameOnPartnerMenu 夥伴選單名稱 | `pInputText` `maxlength=200` | `required`, `maxLength(200)` |
| NameOnCourseDetailPage 課程頁顯示名稱 | `pInputText` `maxlength=50` | `required`, `maxLength(50)` |
| DisplayOrder 顯示順序 | `p-inputnumber [useGrouping]=false [showButtons]=true` | `required`; default `0` |
| ImageFilename 圖片檔名 | `pInputText` `maxlength=50` | optional, `maxLength(50)`, `pattern(/^[\x21-\x7E]+$/)`; blank → `null` |

- Save button disabled while the form is invalid or a request is in flight.
- String values are trimmed before submit.
- No lookups → `forkJoin` only wraps the `getById` call in edit mode.
- On save success navigate to `/course/partners/{pkid}` (create uses the pkid returned in
  the 201 body).

### Date Handling

No date columns. `RowAuditBadgeComponent` appends `'Z'` before formatting audit
`dateTime` (Dapper returns `Kind = Unspecified`).

### Special Form Behaviors

- `pkid` is never editable (IDENTITY). New mode sends `pkid: 0`.
- No cross-field defaults.

### Sub-panels (edit mode only)

**N/A** (a future `PartnerCourseGroup` feature may add an inline sub-panel here).

### Sidebar placement

**New group 課程管理 Course** in `app.ts` `menuItems`, placed after 系統管理 Admin:

```ts
{
  label: '課程管理 Course',
  icon: 'pi pi-book',
  expanded: true,
  items: [
    { label: '合作夥伴 Partner', icon: 'pi pi-building', routerLink: '/course/partners' }
  ]
}
```

`app.html` needs no change (the menu is data-driven).

---

## Tests

### Backend (`CMS.API.Tests`)

`Controllers\PartnersControllerTests.cs` — Moq `IPartnerRepository` (strict):

- `GetAll` returns 200 with the repository list
- `Query` passes the `PartnerQuery` through and returns 200
- `GetById` → 200 when found, 404 when repository returns null
- `Create` → 201 `CreatedAtAction` pointing at `GetById` with the new pkid from the
  repository; body echoes the request with the assigned pkid
- `Update` → 204 when the repository reports a row affected, 404 otherwise
- `Delete` → 204 / 404; 409 when the repository throws `EntityInUseException`
- Validation: `PartnerRequest` with any required string empty, `Name` > 50,
  `AppKey` > 10 or non-ASCII, `NameOnPartnerMenu` > 200, `NameOnCourseDetailPage` > 50,
  `ImageFilename` > 50 or non-ASCII fails `Validator.TryValidateObject`; a null
  `ImageFilename` is valid.

`Controllers\LookupsControllerTests.cs` — add `Partners_ReturnsOk_WithLookupItems`
(constructor gains an `IPartnerRepository` mock).

### Frontend (Karma + Jasmine)

- `core/services/partner.service.spec.ts` — `provideHttpClientTesting()` +
  `HttpTestingController`; asserts URL and verb for all six methods.
- `partner-list.component.spec.ts` — mocked service; renders the table rows, link buttons,
  filter badge, restores/saves session-storage keys, confirms delete with pkid + name.
- `partner-detail.component.spec.ts` — mocked service + `ActivatedRoute` (`id = 1`);
  renders the seven fields, all five link buttons, the row-audit badge, and the 404 state.
- `partner-form.component.spec.ts` — new mode: form invalid until the four names are set;
  `AppKey` rejects non-ASCII; blank `ImageFilename` is sent as `null`; edit mode: loads by
  id, submits with the original pkid, renders the edit title and audit badge.
- `app.spec.ts` — add an assertion that the sidebar lists 課程管理 Course / 合作夥伴 Partner.
- Every component spec provides `provideRouter([])`, `provideNoopAnimations()`,
  `MessageService`, `ConfirmationService`.

---

## Files to Create / Modify

| Area | File | Action |
|------|------|--------|
| API | `src\CMS.API\Models\Partner.cs` | Create |
| API | `src\CMS.API\Models\PartnerRequest.cs` | Create |
| API | `src\CMS.API\Models\PartnerQuery.cs` | Create |
| API | `src\CMS.API\Repositories\IPartnerRepository.cs` | Create |
| API | `src\CMS.API\Repositories\PartnerRepository.cs` | Create |
| API | `src\CMS.API\Controllers\PartnersController.cs` | Create |
| API | `src\CMS.API\Controllers\LookupsController.cs` | Modify — add `partners` action |
| API | `src\CMS.API\Program.cs` | Modify — DI registration |
| Tests | `src\CMS.API.Tests\Controllers\PartnersControllerTests.cs` | Create |
| Tests | `src\CMS.API.Tests\Controllers\LookupsControllerTests.cs` | Modify — partners lookup test |
| NG | `src\app\core\models\partner.model.ts` | Create |
| NG | `src\app\core\services\partner.service.ts` (+ spec) | Create |
| NG | `src\app\core\services\lookup.service.ts` | Modify — `partners()` |
| NG | `src\app\features\partners\partner-list\*` (ts/html/scss/spec) | Create |
| NG | `src\app\features\partners\partner-detail\*` | Create |
| NG | `src\app\features\partners\partner-form\*` | Create |
| NG | `src\app\app.routes.ts` | Modify — lazy routes under `course/partners` |
| NG | `src\app\app.ts` | Modify — new sidebar group 課程管理 Course |
| NG | `src\app\app.spec.ts` | Modify — sidebar assertion |
| Docs | `CLAUDE.md` | Modify — menu + status |

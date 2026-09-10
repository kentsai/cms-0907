# Build Spec for Certification
- database schema: `.\database\course.sql`

---

## Summary

`Certification` is a vendor certification (e.g. Microsoft *Azure Administrator Associate*, Cisco
*CCNA*) that courses lead towards. Each row belongs to exactly one `Partner` and has a single,
nullable `Title`. It has **no child entities** — the two tables that point at it are pure N-N
junctions (`CourseInCertification`, `CertificationJobCategories`) managed with multiselects on
the form. The `certifications` lookup that `Course` already consumes moves out of the temporary
`LookupRepository` into the new `CertificationRepository`.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **int IDENTITY(1,1)** → C# `int`; server-assigned, omitted on create, immutable on update |
| Foreign Keys | `Partner_pkid` (smallint, NOT NULL) → `Partner.pkid` |
| Required Fields | `Partner_pkid` |
| N-N Relationships | `CourseInCertification` (Certification ↔ Course; FK to Certification has **no** cascade), `CertificationJobCategories` (Certification ↔ JobCategory; `ON DELETE CASCADE` on the Certification side) |
| Primary-Foreign Links | N/A — nothing references `Certification.pkid` except the two junctions above |
| Query Filters | keyword (`Title`), `PartnerPkid`, `CoursePkid` (EXISTS on junction), `JobCategoryPkid` (EXISTS on junction) |
| Default Sort | `pkid DESC` (no `DisplayOrder` column; the lookup keeps its partner-then-title order) |

The unique index `IX_Certification (pkid, Partner_pkid)` is a superset of the PK and needs no
handling.

---

## Localization

### Chinese Table Name

- Certification: 認證
- Description: 原廠認證主檔，供課程（可複選）與職務類別（可複選）關聯

### Chinese Column Names

- pkid: 主代碼
- Partner_pkid: 原廠
- Title: 認證名稱

Related labels used on the pages:

- CourseInCertification / Course: 對應課程
- CertificationJobCategories / JobCategory: 職務類別
- Partner.Name (JOINed): 原廠

---

## Required Fields

Required (NOT NULL):
- `Partner_pkid` — smallint, FK → Partner

Optional (nullable):
- `Title` — nchar(100). Stored padded; **always `RTRIM()`** on read, trimmed on write, blank → NULL.

---

## Foreign Keys

- `Partner_pkid` → `Partner.pkid` (smallint). **Not nullable** — the form dropdown has no 無 option
  and is `required`.
  - Option label: `Partner.Name`
  - Order by: `Partner.DisplayOrder ASC, Partner.Name ASC` (what `GET /api/lookups/partners`
    already returns)
  - SQL alias: `c.Partner_pkid AS PartnerPkid`; the JOINed `p.Name AS PartnerName` is carried on
    every row so the list shows the name without a lookup.

---

## Foreign-Primary Links

- `Partner_pkid` → Partner
  - Detail page renders `partnerName` as a link to `/course/partners/{partnerPkid}`
  - List page shows `partnerName` as plain text (the row's 檢視 button is the navigation)
  - Always present (NOT NULL) — no null guard needed

---

## Primary-Foreign Links

**N/A** — `CourseInCertification` and `CertificationJobCategories` are pure junctions (two FK
columns, composite PK, no payload) and are modelled as N-N relationships below. No list page
accepts a `certificationPkid` param.

Incoming navigation: `Partner` list/detail already link to
`/course/certifications?partnerPkid={pkid}` (查看認證 button), so the list page must accept a
`partnerPkid` query param.

---

## N-N Relationships

### CourseInCertification — Certification ↔ Course

- Junction `CourseInCertification(Course_pkid int, Certification_pkid int)`, PK on both.
  `FK_CourseInCertification_Course` cascades; `FK_CourseInCertification_Certification` does
  **not** — see Delete below.
- Related entity **Course**, lookup `GET /api/lookups/courses` (exists; label = `CourseId Title`,
  ordered by `CourseId`). Courses can exceed 100 rows → `[virtualScroll]="true"
  [virtualScrollItemSize]="43"` on the multiselect and the filter dropdown.
- **List**: not shown (would need a per-row subquery; the detail page covers it).
- **Detail**: card 課程與職務類別 → 對應課程 shows the course labels joined by 、, resolved from the
  `courses` lookup; unknown pkid falls back to `#pkid`; empty → —.
- **Form**: `p-multiselect` `display="chip"` `[maxSelectedLabels]="9999"` `[filter]="true"`
  `appendTo="body"`, virtual scroll on.
- Request field `CoursePkids: List<int>`.
- Sync on save (inside the same transaction as the INSERT/UPDATE):
  1. `DELETE FROM CourseInCertification WHERE Certification_pkid = @Pkid`
  2. Bulk `INSERT INTO CourseInCertification (Course_pkid, Certification_pkid) VALUES (@CoursePkid, @Pkid)`
     for each distinct id.

### CertificationJobCategories — Certification ↔ JobCategory

- Junction `CertificationJobCategories(Certification_pkid int, JobCategory_pkid smallint)`,
  PK on both. Cascades on the Certification side.
- Related entity **JobCategory**, lookup `GET /api/lookups/job-categories` (exists in
  `LookupRepository`; label = `Description`).
- **Detail**: 職務類別 labels joined by 、, same fallback rules.
- **Form**: `p-multiselect` `display="chip"` `[maxSelectedLabels]="9999"` `[filter]="true"`
  `appendTo="body"` (no virtual scroll — small table).
- Request field `JobCategoryPkids: List<short>`.
- Sync on save:
  1. `DELETE FROM CertificationJobCategories WHERE Certification_pkid = @Pkid`
  2. Bulk `INSERT INTO CertificationJobCategories (Certification_pkid, JobCategory_pkid) VALUES (@Pkid, @JobCategoryPkid)`.

Both id lists are normalised (`Distinct().OrderBy()`) before the diff and the insert, and are
populated only by `GetByIdAsync` (list/query rows carry empty lists).

---

## Query Filters

- **keyword**: `string?` — `LIKE '%' + @Keyword + '%'` on `RTRIM(c.Title)` only (the sole string
  column). Trimmed; blank → no filter.
- **PartnerPkid**: `short?` — exact match on `c.Partner_pkid`. Dropdown from
  `GET /api/lookups/partners` (label `Name`, `[filter]="true"`, `[showClear]="true"`, placeholder
  全部). Pre-filled from the incoming `partnerPkid` query param.
- **CoursePkid**: `int?` — `EXISTS (SELECT 1 FROM CourseInCertification j WHERE
  j.Certification_pkid = c.pkid AND j.Course_pkid = @CoursePkid)`. Dropdown from
  `GET /api/lookups/courses` with filter + virtual scroll.
- **JobCategoryPkid**: `short?` — `EXISTS (SELECT 1 FROM CertificationJobCategories j WHERE
  j.Certification_pkid = c.pkid AND j.JobCategory_pkid = @JobCategoryPkid)`. Dropdown from
  `GET /api/lookups/job-categories`.

No bool columns and no date columns → no tri-state or date-range filters.

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/partners` | Exists (`PartnerRepository.GetLookupAsync`) | `{ pkid, label = Name }[]` |
| `GET /api/lookups/courses` | Exists (`CourseRepository.GetLookupAsync`) | `{ pkid, label = "CourseId Title" }[]` |
| `GET /api/lookups/job-categories` | Exists (`LookupRepository.GetJobCategoriesAsync`) | `{ pkid, label = Description }[]` |
| `GET /api/lookups/certifications` | **Moves** — from `LookupRepository.GetCertificationsAsync` to `CertificationRepository.GetLookupAsync`; route and payload unchanged | `{ pkid, label = Partner.Name + ' ' + RTRIM(Title) }[]`, ordered `p.DisplayOrder, p.Name, RTRIM(c.Title), c.pkid` |

After the move `ILookupRepository` keeps only `GetJobCategoriesAsync` (until JobCategory is built).
`LookupsController` gains an `ICertificationRepository` constructor parameter; its test class
gains the matching mock. `lookup.service.ts` needs no change (`certifications()` already exists).

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/certifications` | List all, `ORDER BY c.pkid DESC`, rows carry `partnerName`, empty id lists |
| `POST` | `/api/certifications/query` | Filtered query (body: `CertificationQuery`) |
| `GET` | `/api/certifications/{id:int}` | Get by pkid with both id lists → 200 / 404 |
| `POST` | `/api/certifications` | Create. `pkid` in the body is ignored; row + junctions inserted in one transaction; re-read and returned → 201 `CreatedAtAction(GetById)` |
| `PUT` | `/api/certifications` | Update (pkid from body) incl. junction resync → 204 / 404 |
| `DELETE` | `/api/certifications/{id:int}` | Delete → 204 / 404. Both junction sets are removed in the same transaction (see Backend Notes); a residual FK violation still maps to **409 Conflict** |
| `GET` | `/api/lookups/certifications` | Slim lookup list (moved, see above) |

Notes:
- Same controller shape as `CoursesController` (`[ApiController]`, `[Produces("application/json")]`,
  `ActionResult<T>`, `CancellationToken` on every action).
- Auth: a Bearer JWT is required on every action by the global `AuthorizeFilter`. No Admin policy on
  this feature — every signed-in user may read and write it.

---

## Backend Notes

### Models

```csharp
// Models/Certification.cs
public class Certification
{
    public int Pkid { get; set; }
    public short PartnerPkid { get; set; }
    public string? Title { get; set; }                       // nchar(100) NULL, RTRIMmed

    /// <summary>Partner.Name (INNER JOIN).</summary>
    public string PartnerName { get; set; } = string.Empty;

    /// <summary>Linked Course.pkid values via CourseInCertification (GetById only).</summary>
    public List<int> CoursePkids { get; set; } = [];

    /// <summary>Linked JobCategory.pkid values via CertificationJobCategories (GetById only).</summary>
    public List<short> JobCategoryPkids { get; set; } = [];
}

// Models/CertificationRequest.cs
public class CertificationRequest
{
    public int Pkid { get; set; }                            // ignored on POST, row key on PUT

    [Range(1, short.MaxValue, ErrorMessage = "請選擇原廠。")]
    public short PartnerPkid { get; set; }

    [StringLength(100)]
    public string? Title { get; set; }

    public List<int> CoursePkids { get; set; } = [];
    public List<short> JobCategoryPkids { get; set; } = [];
}

// Models/CertificationQuery.cs
public class CertificationQuery
{
    public string? Keyword { get; set; }        // LIKE on Title
    public short? PartnerPkid { get; set; }
    public int? CoursePkid { get; set; }        // EXISTS on CourseInCertification
    public short? JobCategoryPkid { get; set; } // EXISTS on CertificationJobCategories
}
```

### SQL — SELECT

Shared by `GetAllAsync` / `QueryAsync` / `GetByIdAsync`:

```sql
SELECT c.pkid, c.Partner_pkid AS PartnerPkid, RTRIM(c.Title) AS Title,
       p.Name AS PartnerName
FROM Certification c
INNER JOIN Partner p ON p.pkid = c.Partner_pkid
-- QueryAsync appends:
-- WHERE (@Keyword IS NULL OR RTRIM(c.Title) LIKE '%' + @Keyword + '%')
--   AND (@PartnerPkid IS NULL OR c.Partner_pkid = @PartnerPkid)
--   AND (@CoursePkid IS NULL OR EXISTS (SELECT 1 FROM CourseInCertification j
--                                       WHERE j.Certification_pkid = c.pkid AND j.Course_pkid = @CoursePkid))
--   AND (@JobCategoryPkid IS NULL OR EXISTS (SELECT 1 FROM CertificationJobCategories j
--                                            WHERE j.Certification_pkid = c.pkid AND j.JobCategory_pkid = @JobCategoryPkid))
ORDER BY c.pkid DESC
```

No multi-map: `PartnerName` is a flat column. `GetByIdAsync` then runs two extra queries on the
same connection/transaction:

```sql
SELECT Course_pkid      FROM CourseInCertification      WHERE Certification_pkid = @Pkid ORDER BY Course_pkid;
SELECT JobCategory_pkid FROM CertificationJobCategories WHERE Certification_pkid = @Pkid ORDER BY JobCategory_pkid;
```

### SQL — INSERT

```sql
INSERT INTO Certification (Partner_pkid, Title)
VALUES (@PartnerPkid, @Title);
SELECT CAST(SCOPE_IDENTITY() AS int);
```

Then the two N-N syncs, then the RowAudit row, then commit.

### SQL — UPDATE

```sql
UPDATE Certification
SET Partner_pkid = @PartnerPkid,
    Title = @Title
WHERE pkid = @Pkid;
```

Reads the existing row first (same transaction) → 404 when missing; resyncs both junctions; reloads the
row and audits the diff of the two snapshots (`LogUpdateAsync`): changed scalar columns plus `CoursePkids` /
`JobCategoryPkids` when the id sets differ; no audit row when nothing differs.

### SQL — DELETE

```sql
DELETE FROM CourseInCertification      WHERE Certification_pkid = @Pkid;   -- no cascade on this FK
DELETE FROM CertificationJobCategories WHERE Certification_pkid = @Pkid;   -- cascades anyway; explicit for symmetry
DELETE FROM Certification WHERE pkid = @Pkid;
```

**Decision:** the junction rows are payload-free links that this feature owns through its
multiselects, so deleting a certification removes its links rather than refusing with 409. A
`SqlException` 547 is still caught and surfaced as `EntityInUseException` → 409 as a safety net.

### N-N Sync Pattern

See *N-N Relationships*. Both syncs are `DELETE` then a bulk `INSERT` of the normalised list,
identical in shape to `CourseRepository.SyncCertificationsAsync`.

### RowAudit

`IRowAuditWriter.LogInsertAsync / LogUpdateAsync / LogDeleteAsync` on the same open connection/transaction
(`PartnerName` is `[AuditIgnore]`d on the model):

| Action | `TableName` | `PrimaryKeyValues` | `ActionType` | `ActionDesc` |
|--------|-------------|--------------------|--------------|--------------|
| Create | `Certification` | new `pkid` | `INSERT` | trimmed `Title` (NULL when untitled) |
| Update | `Certification` | `pkid` | `UPDATE` | comma-separated changed members; no row when nothing changed |
| Delete | `Certification` | `pkid` | `DELETE` | `Title` of the deleted row (NULL when untitled) |

### Special Column Notes

- `Title` is **`nchar(100)`** → `RTRIM(c.Title)` in every SELECT (including the keyword LIKE and
  the lookup label); write side trims and converts blank to NULL.
- `Partner_pkid` is `smallint` → `short` / `short?`; TypeScript `number`.
- `JobCategory_pkid` in the junction is `smallint` → `List<short>`; Dapper maps the multiselect's
  `number[]` fine because the JSON binder targets `short`.
- No computed, `varchar`, `date`, `time`, or default-constrained columns.

---

## Frontend Notes

### Routes

Lazy `loadComponent` entries in `app.routes.ts`, appended after `course/courses` (`/new` before `/:id`):

| Route | Component |
|-------|-----------|
| `/course/certifications` | `CertificationListComponent` |
| `/course/certifications/new` | `CertificationFormComponent` (new mode) |
| `/course/certifications/:id` | `CertificationDetailComponent` |
| `/course/certifications/:id/edit` | `CertificationFormComponent` (edit mode) |

### Model (`core/models/certification.model.ts`)

```ts
export interface Certification {
  pkid: number;
  partnerPkid: number;
  title: string | null;
  partnerName: string;          // JOINed label
  coursePkids: number[];        // populated by GET /{id} only
  jobCategoryPkids: number[];   // populated by GET /{id} only
}
export type CertificationRequest = Omit<Certification, 'partnerName'>;
export interface CertificationQuery {
  keyword?: string | null;
  partnerPkid?: number | null;
  coursePkid?: number | null;
  jobCategoryPkid?: number | null;
}
export const EMPTY_CERTIFICATION_QUERY: CertificationQuery = { keyword: null, partnerPkid: null, coursePkid: null, jobCategoryPkid: null };
```

### Service (`core/services/certification.service.ts`)

`getAll()`, `query(q)`, `getById(id)`, `create(req)` (returns the re-read `Certification`),
`update(req)` (PUT, pkid in body), `delete(id)` against `${environment.apiBaseUrl}/certifications`.
Numeric PK → no `encodeURIComponent`.

### List page (`features/certifications/certification-list/`)

- Header **認證 Certification**, subtitle 原廠認證主檔; toolbar 搜尋條件 (drawer, badge = active
  filter count) and 新增 (`/course/certifications/new`).
- `p-table` (`p-datatable-sm`, paginator top, rows `[10, 20, 50]` default 20,
  `{first}–{last} 筆，共 {totalRecords} 筆`) columns, sortable unless noted:
  主代碼 (`pkid`), 原廠 (`partnerName`), 認證名稱 (`title`, shows — when null), 操作 (view / edit /
  delete, not sortable).
- Default sort `pkid` descending (`sortOrder = -1`), matching the API order.
- Filter drawer (`p-drawer` right, `appendTo="body"` on every select): 關鍵字 text (placeholder
  認證名稱), 原廠 `p-select` (filter + clear), 課程 `p-select` (filter + clear + virtual scroll),
  職務類別 `p-select` (clear). Buttons 清除 / 查詢.
- Lookups loaded with `forkJoin` on init (partners, courses, jobCategories); the table loads
  independently because `partnerName` is on the row.
- Delete via `p-confirmdialog`; success toast + reload; 409 shows the API message.

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.title ?? '(無名稱)'}」？
```

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `certification-list-filters` | Last `CertificationQuery` values |
| `certification-list-sort` | `{ sortField, sortOrder }` |
| `certification-list-page` | `{ first, rows }` |

Incoming query param `partnerPkid` (from the Partner 查看認證 buttons) overrides the saved
`partnerPkid` filter on init.

### Lookup Binding in List

FK label comes on the row (`partnerName`). The three lookups only feed the filter drawer.

### Detail page (`features/certifications/certification-detail/`)

- Sticky `p-toolbar`: `#start` = 認證 + `pkid　partnerName　title` subtitle +
  `RowAuditBadgeComponent` (`tableName="Certification"`, `[pkid]="pkid"`); `#end` = 返回, 編輯, 刪除.
- `forkJoin` of `getById`, `lookup.courses()`, `lookup.jobCategories()`.
- Card 基本資料: 主代碼, 原廠 (link `a.fk-link` → `/course/partners/{partnerPkid}`), 認證名稱 (— when null).
- Card 課程與職務類別: 對應課程 and 職務類別 label lists joined by 、 (fallback `#pkid`, empty → —).
- 404 → 找不到主代碼 {pkid} 的認證。

### Form page (`features/certifications/certification-form/`)

Reactive Forms; sticky `p-toolbar` (新增認證 / 編輯認證 + 主代碼 + audit badge in edit mode;
取消 / 儲存 on the end, 儲存 disabled while invalid / saving / loading).

| Field | Control | Validators / behaviour |
|-------|---------|------------------------|
| pkid 主代碼 | toolbar text (edit only) | not a control |
| partnerPkid 原廠 | `p-select` filter, placeholder 請選擇原廠 | `required` |
| title 認證名稱 | `pInputText maxlength=100`, placeholder 選填 | `maxLength(100)`; trimmed, blank → `null` |
| coursePkids 對應課程 | `p-multiselect` chip, filter, virtual scroll | none |
| jobCategoryPkids 職務類別 | `p-multiselect` chip, filter | none |

- `forkJoin` of partners / courses / jobCategories lookups + `getById` (edit) or `of(null)` (new).
- New mode sends `pkid: 0`; create response gives the assigned pkid; both modes navigate to
  `/course/certifications/{pkid}` and toast `主代碼 {pkid}「{title ?? '(無名稱)'}」已儲存。`.

### Date Handling

No date columns. The audit badge appends `'Z'` itself.

### Special Form Behaviors

- None beyond the trim/blank→null rule on `title`. No cross-field defaults.

### Sub-panels (edit mode only)

**N/A**

### Sidebar placement

**Existing group 課程管理 Course** in `app.ts` `menuItems`, appended after 課程 Course:

```ts
{ label: '認證 Certification', icon: 'pi pi-verified', routerLink: '/course/certifications' }
```

`app.html` needs no change (the menu is data-driven).

---

## Tests

### Backend (`CMS.API.Tests`)

`Controllers\CertificationsControllerTests.cs` — strict Moq `ICertificationRepository`:

- `GetAll` → 200 with the repository list
- `Query` passes the `CertificationQuery` through (keyword + partner + course + job category) → 200
- `GetById` → 200 when found, 404 when null
- `Create` → 201 `CreatedAtAction(GetById)` with the re-read row; falls back to echoing the request
  (with the new pkid and both id lists) when the re-read returns null
- `Update` → 204 / 404
- `Delete` → 204 / 404 / 409 on `EntityInUseException`
- Validation: `PartnerPkid = 0` fails; `Title` of 101 chars fails; `Title = null`, empty id lists
  and a 100-char title are valid.

`Controllers\LookupsControllerTests.cs` — constructor gains `Mock<ICertificationRepository>`;
`Certifications_ReturnsOk_WithLookupItems` now sets up `_certifications.GetLookupAsync` instead of
`_lookups.GetCertificationsAsync`.

### Frontend (Karma + Jasmine)

- `core/services/certification.service.spec.ts` — `provideHttpClientTesting()`; URL + verb for all
  six methods; create body carries `pkid: 0` and both id lists; update PUTs to the root.
- `certification-list.component.spec.ts` — mocked `CertificationService` + `LookupService`
  (`partners`, `courses`, `jobCategories`); renders rows with `partnerName` and — for a null title;
  header order 主代碼 / 原廠 / 認證名稱 / 操作; incoming `partnerPkid` param overrides the saved
  filter; active-filter badge count; search persists filters and resets paging; delete confirm
  contains `<b>pkid</b>` and the title (or `(無名稱)`).
- `certification-detail.component.spec.ts` — mocked services + `RowAuditService` +
  `ActivatedRoute` (`id = 1`); renders fields, the partner `a.fk-link`, resolved course / job
  category labels with `#pkid` fallback, the audit badge, the 404 state, and the delete confirm.
- `certification-form.component.spec.ts` — new mode: three lookups loaded, invalid until
  `partnerPkid` is set, 101-char title rejected, create called with `pkid: 0`, trimmed title,
  `null` for a blank title, both id lists, navigation to the new pkid; edit mode: loads by id,
  patches all controls, submits with the original pkid, renders 編輯認證 + 主代碼 + audit badge.
- `app.spec.ts` — add `should list Certification under the Course menu group`.
- Every component spec provides `provideRouter([])`, `provideNoopAnimations()`, `MessageService`,
  `ConfirmationService`.

---

## Files to Create / Modify

| Area | File | Action |
|------|------|--------|
| API | `src\CMS.API\Models\Certification.cs` | Create |
| API | `src\CMS.API\Models\CertificationRequest.cs` | Create |
| API | `src\CMS.API\Models\CertificationQuery.cs` | Create |
| API | `src\CMS.API\Repositories\ICertificationRepository.cs` | Create |
| API | `src\CMS.API\Repositories\CertificationRepository.cs` | Create (includes `GetLookupAsync`) |
| API | `src\CMS.API\Repositories\ILookupRepository.cs` / `LookupRepository.cs` | Modify — remove `GetCertificationsAsync` |
| API | `src\CMS.API\Controllers\CertificationsController.cs` | Create |
| API | `src\CMS.API\Controllers\LookupsController.cs` | Modify — `certifications` action uses `ICertificationRepository` |
| API | `src\CMS.API\Program.cs` | Modify — DI registration |
| Tests | `src\CMS.API.Tests\Controllers\CertificationsControllerTests.cs` | Create |
| Tests | `src\CMS.API.Tests\Controllers\LookupsControllerTests.cs` | Modify — new mock, certifications test |
| NG | `src\app\core\models\certification.model.ts` | Create |
| NG | `src\app\core\services\certification.service.ts` (+ spec) | Create |
| NG | `src\app\features\certifications\certification-list\*` (ts/html/scss/spec) | Create |
| NG | `src\app\features\certifications\certification-detail\*` | Create |
| NG | `src\app\features\certifications\certification-form\*` | Create |
| NG | `src\app\app.routes.ts` | Modify — lazy routes under `course/certifications` |
| NG | `src\app\app.ts` | Modify — sidebar entry under 課程管理 Course |
| NG | `src\app\app.spec.ts` | Modify — sidebar assertion |
| Docs | `CLAUDE.md`, `docs\claude\feature-status.md`, `docs\claude\feature-infrastructure.md` | Modify — status, lookup ownership, menu |

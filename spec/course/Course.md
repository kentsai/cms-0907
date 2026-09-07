# Build Spec for Course
- database schema: `.\database\course.sql`

---

## Summary

`Course` is the central entity of the system: a training course sold under a partner brand.
It carries identifying codes, scheduling dates, pricing, long descriptive text, and links to
three lookup tables (`Partner`, `CourseGroup`, `PublishStatus`). It has two pure N-N junctions
(`CourseInCertification`, `CourseJobCategories`) and is the parent of `CourseFAQ`,
`CourseRelatedLink`, `HotCourse` (FK) and `CourseRecomm` (by `CourseId`, no FK).

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **int IDENTITY(1,1)**; server-assigned, omitted on create, immutable on update |
| Foreign Keys | `Partner_pkid` → `Partner.pkid` (smallint), `CourseGroup_pkid` → `CourseGroup.pkid` (smallint, **nullable**), `PublishStatus_pkid` → `PublishStatus.pkid` (tinyint) |
| Required Fields | `Title`, `CourseId`, `ProdCourseId`, `FriendlyUrl`, `DisplayOrder`, `Partner_pkid`, `PublishStatus_pkid`, `ScheduleOn`, `ScheduleOff`, `Hour`, `ListPrice`, `LearningCredit`, `CanRepeat` |
| N-N Relationships | `CourseInCertification` (Course ↔ Certification, `ON DELETE CASCADE`), `CourseJobCategories` (Course ↔ JobCategory, `ON DELETE CASCADE`) |
| Primary-Foreign Links | `CourseFAQ.Course_pkid`, `CourseRelatedLink.Course_pkid`, `HotCourse.Course_pkid`, `CourseRecomm.CourseId` (varchar match, no FK) |
| Query Filters | keyword (`Title`, `OfficialTitle`, `CourseId`, `ProdCourseId`, `FriendlyUrl`), `PartnerPkid`, `CourseGroupPkid`, `PublishStatusPkid`, `ScheduleOn` range, `ScheduleOff` range, `CanRepeat` |
| Default Sort | `DisplayOrder ASC, pkid DESC` |

**Out of scope for this iteration** (mentioned in the reference sample spec but not part of the
standard `/crud` deliverable): the `POST /copy` action, QR-code rendering, print-to-PDF, and the
inline `CourseRelatedLink` / `CourseRecomm` sub-panels. `ClassSection` does not exist in
`database\course.sql`, so no 查看開課時間 link is generated.

---

## Localization

### Chinese Table Name

- Course: 課程
- Description: 訓練課程主檔（代碼、名稱、原廠、群組、上下架、時數、定價、課程內容）

### Chinese Column Names

- pkid: 主代碼
- Title: 課程名稱
- OfficialTitle: 官方課程名稱
- CourseId: 簡介代碼
- ProdCourseId: 科目代碼
- FriendlyUrl: 友善網址
- DisplayOrder: 顯示順序
- Partner_pkid: 原廠
- CourseGroup_pkid: 課程群組
- PublishStatus_pkid: 上架狀態
- ScheduleOn: 上架日期
- ScheduleOff: 下架日期
- Hour: 時數
- ListPrice: 定價
- LearningCredit: 點數
- Material: 教材
- Objective: 課程目標
- Target: 適合對象
- Prerequisites: 先備知識
- Outline: 課程大綱
- TowardCertOrExam: 考試／認證說明
- Note: 備註
- OtherInfo: 其他資訊
- CanRepeat: 允許重聽

(Column labels above follow the user-supplied list-page hints: 簡介代碼, 科目代碼, 原廠,
上架狀態, 點數, 允許重聽.)

---

## Required Fields

Required (NOT NULL):
- `Title` — nvarchar(200)
- `CourseId` — varchar(50). **ASCII only** (printable, no whitespace) on both sides.
- `ProdCourseId` — varchar(50). Same ASCII rule.
- `FriendlyUrl` — nvarchar(100)
- `DisplayOrder` — int
- `Partner_pkid` — smallint (must be ≥ 1)
- `PublishStatus_pkid` — tinyint (must be ≥ 1)
- `ScheduleOn`, `ScheduleOff` — date; `ScheduleOff` must be ≥ `ScheduleOn`
- `Hour` — smallint (≥ 0)
- `ListPrice` — decimal(9,0) (0 … 999,999,999)
- `LearningCredit` — decimal(9,1) (0 … 99,999,999.9)
- `CanRepeat` — bit

Optional (nullable):
- `OfficialTitle` nvarchar(300), `CourseGroup_pkid` smallint, `Material` nvarchar(500),
  `Objective` nvarchar(4000), `Target` nvarchar(500), `Prerequisites` nvarchar(4000),
  `Outline` nvarchar(max), `TowardCertOrExam` nvarchar(max), `Note` nvarchar(4000),
  `OtherInfo` nvarchar(4000). Blank strings are normalised to `null`.

---

## Foreign Keys

- **Partner_pkid** → `Partner.pkid` (smallint, NOT NULL)
  - Alias `Partner_pkid AS PartnerPkid`; JOIN label `Partner.Name AS PartnerName`
  - Lookup `GET /api/lookups/partners` (label = Name, ordered by DisplayOrder, Name)
- **CourseGroup_pkid** → `CourseGroup.pkid` (smallint, **nullable** → dropdown has 清除／無)
  - Alias `CourseGroup_pkid AS CourseGroupPkid`; LEFT JOIN label `CourseGroup.Description AS CourseGroupDescription`
  - Lookup `GET /api/lookups/course-groups` (label = Description)
- **PublishStatus_pkid** → `PublishStatus.pkid` (tinyint, NOT NULL)
  - Alias `PublishStatus_pkid AS PublishStatusPkid`; JOIN label `PublishStatus.Description AS PublishStatusDescription`
  - Lookup `GET /api/lookups/publish-statuses` (label = Description)

The three label columns are returned by the API on every row, so the list page needs no
lookup calls for display (only for the filter dropdowns).

---

## Foreign-Primary Links

Shown on the detail page next to the FK value:

- `PartnerPkid` → `/course/partners/{partnerPkid}`
- `CourseGroupPkid` → `/course/course-groups/{courseGroupPkid}` (only when not null)
- `PublishStatusPkid` → `/admin/publish-statuses/{publishStatusPkid}`

---

## Primary-Foreign Links

Shown on the **detail page only** (the list page uses the user-prescribed column set and is
already wide). None of the target list pages exist yet; routes are the agreed conventions.

- **CourseFAQ** (`Course_pkid`) — 查看課程問答 (`pi pi-question-circle`) → `/course/course-faqs?coursePkid={pkid}`
- **CourseRelatedLink** (`Course_pkid`) — 查看相關連結 (`pi pi-link`) → `/course/course-related-links?coursePkid={pkid}`
- **HotCourse** (`Course_pkid`) — 查看熱門課程 (`pi pi-star`) → `/course/hot-courses?coursePkid={pkid}`
- **CourseRecomm** (`CourseId`, no FK) — 查看推薦課程 (`pi pi-thumbs-up`) → `/course/course-recomms?courseId={courseId}`

---

## N-N Relationships

### CourseInCertification — Course ↔ Certification

- Junction `CourseInCertification(Course_pkid int, Certification_pkid int)`, PK on both.
- Lookup `GET /api/lookups/certifications` — **new**, lives in `LookupRepository` until a
  Certification feature exists. Label = `Partner.Name + ' ' + RTRIM(Certification.Title)`
  (`Title` is `nchar(100)` → `RTRIM`), ordered by Partner DisplayOrder, Partner Name, Title.
- Form: `p-multiselect` with `[virtualScroll]` (certification lists can be long).
- Detail: labels resolved client-side from the lookup.
- Request field `CertificationPkids: List<int>`.

### CourseJobCategories — Course ↔ JobCategory

- Junction `CourseJobCategories(Course_pkid int, JobCategory_pkid smallint)`.
- Lookup `GET /api/lookups/job-categories` — **new**, in `LookupRepository`. Label =
  `Description`, ordered by Description.
- Form: `p-multiselect`. Detail: labels from the lookup.
- Request field `JobCategoryPkids: List<short>`.

Sync on create/update (same transaction): `DELETE … WHERE Course_pkid = @Pkid` then bulk
`INSERT` of the distinct submitted ids. Both junctions have `ON DELETE CASCADE`, so deleting
a course needs no manual junction cleanup.

---

## Query Filters

- **keyword** — LIKE on `Title`, `OfficialTitle`, `CourseId`, `ProdCourseId`, `FriendlyUrl`
  (large text columns excluded).
- **partnerPkid** (short?) — exact match; `p-select` from `/api/lookups/partners`.
- **courseGroupPkid** (short?) — exact match; `p-select` from `/api/lookups/course-groups`.
- **publishStatusPkid** (byte?) — exact match; `p-select` from `/api/lookups/publish-statuses`.
- **scheduleOnFrom / scheduleOnTo** (DateOnly?) — inclusive range on `ScheduleOn`.
- **scheduleOffFrom / scheduleOffTo** (DateOnly?) — inclusive range on `ScheduleOff`.
- **canRepeat** (bool?) — tri-state 全部／是／否.

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/partners` | Exists | label = Name |
| `GET /api/lookups/course-groups` | Exists | label = Description |
| `GET /api/lookups/publish-statuses` | Exists | label = Description |
| `GET /api/lookups/certifications` | **New** (`LookupRepository`) | label = `Partner.Name + ' ' + RTRIM(Title)` |
| `GET /api/lookups/job-categories` | **New** (`LookupRepository`) | label = Description |
| `GET /api/lookups/courses` | **New** (`CourseRepository`) | label = `CourseId + ' ' + Title`, ordered by CourseId — for future CourseFAQ / CourseRelatedLink / HotCourse forms |

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/courses` | List all (with FK labels), `ORDER BY DisplayOrder ASC, pkid DESC` |
| `POST` | `/api/courses/query` | Filtered query (body: `CourseQuery`) |
| `GET` | `/api/courses/{id:int}` | Get by pkid, includes `certificationPkids` / `jobCategoryPkids` → 200 / 404 |
| `POST` | `/api/courses` | Create; syncs both junctions; returns the re-read row → 201 `CreatedAtAction` |
| `PUT` | `/api/courses` | Update (pkid from body); syncs both junctions → 204 / 404 |
| `DELETE` | `/api/courses/{id:int}` | Delete → 204 / 404; FK violation from CourseFAQ / CourseRelatedLink / HotCourse → **409** |
| `GET` | `/api/lookups/courses` | Slim lookup |

No `[Authorize]` attributes.

---

## Backend Notes

### Models

```csharp
public class Course
{
    public int Pkid { get; set; }
    public string Title { get; set; } = "";
    public string? OfficialTitle { get; set; }
    public string CourseId { get; set; } = "";
    public string ProdCourseId { get; set; } = "";
    public string FriendlyUrl { get; set; } = "";
    public int DisplayOrder { get; set; }
    public short PartnerPkid { get; set; }
    public short? CourseGroupPkid { get; set; }
    public byte PublishStatusPkid { get; set; }
    public DateOnly ScheduleOn { get; set; }
    public DateOnly ScheduleOff { get; set; }
    public short Hour { get; set; }
    public decimal ListPrice { get; set; }
    public decimal LearningCredit { get; set; }
    public string? Material, Objective, Target, Prerequisites, Outline, TowardCertOrExam, Note, OtherInfo;
    public bool CanRepeat { get; set; }
    // JOINed labels
    public string PartnerName { get; set; } = "";
    public string? CourseGroupDescription { get; set; }
    public string PublishStatusDescription { get; set; } = "";
    // N-N (GetById only)
    public List<int> CertificationPkids { get; set; } = [];
    public List<short> JobCategoryPkids { get; set; } = [];
}

public class CourseRequest : IValidatableObject   // scalars + CertificationPkids + JobCategoryPkids
{
    // [Required][StringLength(200)] Title; [StringLength(300)] OfficialTitle;
    // [Required][StringLength(50)][RegularExpression(ASCII)] CourseId, ProdCourseId;
    // [Required][StringLength(100)] FriendlyUrl; [Range(1, short.MaxValue)] PartnerPkid;
    // [Range(1, byte.MaxValue)] PublishStatusPkid; [Range(0, short.MaxValue)] Hour;
    // [Range(0, 999_999_999)] ListPrice; [Range(0, 99_999_999.9)] LearningCredit;
    // Validate(): ScheduleOff >= ScheduleOn
}

public class CourseQuery
{
    public string? Keyword; public short? PartnerPkid; public short? CourseGroupPkid; public byte? PublishStatusPkid;
    public DateOnly? ScheduleOnFrom, ScheduleOnTo, ScheduleOffFrom, ScheduleOffTo; public bool? CanRepeat;
}
```

### SQL — SELECT

```sql
SELECT c.pkid, c.Title, c.OfficialTitle, c.CourseId, c.ProdCourseId, c.FriendlyUrl, c.DisplayOrder,
       c.Partner_pkid AS PartnerPkid, c.CourseGroup_pkid AS CourseGroupPkid, c.PublishStatus_pkid AS PublishStatusPkid,
       c.ScheduleOn, c.ScheduleOff, c.Hour, c.ListPrice, c.LearningCredit,
       c.Material, c.Objective, c.Target, c.Prerequisites, c.Outline, c.TowardCertOrExam, c.Note, c.OtherInfo, c.CanRepeat,
       p.Name AS PartnerName, g.Description AS CourseGroupDescription, s.Description AS PublishStatusDescription
FROM Course c
INNER JOIN Partner p ON p.pkid = c.Partner_pkid
LEFT JOIN CourseGroup g ON g.pkid = c.CourseGroup_pkid
INNER JOIN PublishStatus s ON s.pkid = c.PublishStatus_pkid
-- QueryAsync WHERE: keyword OR-block, then one (@X IS NULL OR c.Col = @X) per filter,
--   (@ScheduleOnFrom IS NULL OR c.ScheduleOn >= @ScheduleOnFrom) … etc.
ORDER BY c.DisplayOrder ASC, c.pkid DESC
```

`GetByIdAsync` additionally runs
`SELECT Certification_pkid FROM CourseInCertification WHERE Course_pkid = @Pkid` and
`SELECT JobCategory_pkid FROM CourseJobCategories WHERE Course_pkid = @Pkid` on the same connection.

### SQL — INSERT

All 23 writable columns (see sample); `SELECT CAST(SCOPE_IDENTITY() AS int)`; then sync both
junctions; audit `INSERT` with `CourseId`.

### SQL — UPDATE

Same 23 columns `WHERE pkid = @Pkid`; then sync both junctions; audit `UPDATE` with the
changed scalar column names (`AuditHelper.ChangedColumns` against a scalar-only parameter
object) plus `CertificationPkids` / `JobCategoryPkids` when the id sets differ.

### SQL — DELETE

`DELETE FROM Course WHERE pkid = @Pkid` — junctions cascade. SQL error 547 (CourseFAQ,
CourseRelatedLink, HotCourse) → `EntityInUseException` → 409. Audit `DELETE` with `CourseId`.

### Special Column Notes

- `CourseId`, `ProdCourseId` are `varchar` → ASCII-only validation (shared `asciiValidator`).
- `ScheduleOn` / `ScheduleOff` are `date` → C# `DateOnly`. **Dapper 2.1.79 maps `DateOnly`
  natively** (parameters and readers), so no custom type handler is registered.
- `ListPrice` decimal(9,0), `LearningCredit` decimal(9,1) → C# `decimal`; UI uses
  `p-inputnumber` with 0 / 1 fraction digits.
- Default constraints (`Hour`, `ListPrice`, `LearningCredit`, `CanRepeat` = 0) match the C#
  defaults; the form pre-fills them.

---

## Frontend Notes

### Routes

| Route | Component |
|-------|-----------|
| `/course/courses` | `CourseListComponent` |
| `/course/courses/new` | `CourseFormComponent` |
| `/course/courses/:id` | `CourseDetailComponent` |
| `/course/courses/:id/edit` | `CourseFormComponent` |

### Model (`core/models/course.model.ts`)

`Course` mirrors the C# response (dates as `'yyyy-MM-dd'` strings, labels included, pkid
lists). `CourseRequest = Omit<Course, 'partnerName' | 'courseGroupDescription' |
'publishStatusDescription'>`. `CourseQuery` mirrors the C# query with ISO date strings.

### Shared utilities added

- `core/utils/date.util.ts` — `toIso(Date)` (local components, never `toISOString`),
  `fromIso(string)`, `addYears(Date, n)`.
- `core/utils/ascii.validator.ts` — `ASCII_PATTERN` + `asciiValidator`, moved out of
  `partner.model.ts` (which now re-exports them).

### List page (`features/courses/course-list/`) — columns exactly as requested

| Field | Header | Notes |
|-------|--------|-------|
| pkid | 主代碼 | |
| displayOrder | 顯示順序 | |
| courseId | 簡介代碼 | `<code>` |
| prodCourseId | 科目代碼 | `<code>` |
| title | 課程名稱 | |
| partnerName | 原廠 | JOINed `Partner.Name` |
| courseGroupDescription | 課程群組 | JOINed `CourseGroup.Description`, `—` when null |
| publishStatusDescription | 上架狀態 | JOINed `PublishStatus.Description` |
| scheduleOn | 上架日期 | ISO string as-is |
| scheduleOff | 下架日期 | |
| hour | 時數 | |
| listPrice | 定價 | `number:'1.0-0'` |
| learningCredit | 點數 | `number:'1.1-1'` |
| canRepeat | 允許重聽 | 是／否 |
| — | 操作 | view / edit / delete |

- Default sort `displayOrder ASC`; paginator 10/20/50, default 20.
- Filter drawer: 關鍵字, 原廠 (`p-select`), 課程群組 (`p-select`), 上架狀態 (`p-select`),
  上架日期 from/to (`p-datepicker`), 下架日期 from/to, 允許重聽 (全部／是／否).
  Dropdown options load via `forkJoin` of three lookups in parallel with the list query.
- Incoming query params `partnerPkid` and `courseGroupPkid` (from the Partner / CourseGroup
  link buttons) override the saved filter of the same name.

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.courseId}」？
```

### Session Storage Keys

`course-list-filters`, `course-list-sort`, `course-list-page`.

### Detail page (`features/courses/course-detail/`)

Toolbar (title 課程 + `pkid　CourseId Title` + audit badge; 返回／編輯／刪除). Cards:
基本資料 (identifiers, FK values as links, dates, numbers, 允許重聽), 課程內容 (eight text
blocks, `white-space: pre-wrap`, `—` when null), 認證與職務類別 (labels resolved from the
certification / job-category lookups, joined with `、`), 相關資料 (four link buttons).

### Form page (`features/courses/course-form/`)

Reactive Forms; `forkJoin` of five lookups + `getById` (edit). Cards: 基本資料, 課程內容,
認證與職務類別.

| Field | Control | Validators |
|-------|---------|------------|
| title | `pInputText` | required, maxLength 200 |
| officialTitle | `pInputText` | maxLength 300 |
| courseId / prodCourseId | `pInputText` | required, maxLength 50, ascii |
| friendlyUrl | `pInputText` | required, maxLength 100 |
| displayOrder | `p-inputnumber` | required; default 0 |
| partnerPkid | `p-select` (filter) | required |
| courseGroupPkid | `p-select` (showClear) | optional |
| publishStatusPkid | `p-select` | required |
| scheduleOn / scheduleOff | `p-datepicker` `yy-mm-dd` | required; group validator `scheduleOff ≥ scheduleOn` |
| hour | `p-inputnumber` | required, min 0 |
| listPrice | `p-inputnumber` 0 decimals | required, min 0 |
| learningCredit | `p-inputnumber` 1 decimal | required, min 0 |
| material … otherInfo | `pTextarea` autoResize | maxLength per column (none for `max` columns) |
| canRepeat | `p-checkbox binary` | — |
| certificationPkids | `p-multiselect` virtualScroll chips | — |
| jobCategoryPkids | `p-multiselect` chips | — |

### Special Form Behaviors

- **ScheduleOff auto-default**: when `scheduleOn` changes to a `Date`, `scheduleOff` is set to
  `addYears(scheduleOn, 10)` with `{ emitEvent: false }`. New mode pre-fills today / today+10y.
  In edit mode `patchValue` lists `scheduleOn` before `scheduleOff`, so the stored value wins.
- Blank optional strings → `null`; `CourseId` / `ProdCourseId` trimmed.

### Sidebar placement

Existing group 課程管理 Course, appended after 課程群組:
`{ label: '課程 Course', icon: 'pi pi-graduation-cap', routerLink: '/course/courses' }`.

---

## Tests

### Backend (`CMS.API.Tests`)

`Controllers\CoursesControllerTests.cs` — Moq `ICourseRepository` (strict): GetAll, Query,
GetById 200/404, Create 201 (re-read row), Update 204/404, Delete 204/404/409, and
`CourseRequest` validation (blank required strings, non-ASCII `CourseId`, `Title` > 200,
`PartnerPkid` = 0, `PublishStatusPkid` = 0, negative `ListPrice`, `ScheduleOff` < `ScheduleOn`).
`LookupsControllerTests.cs` gains certifications, job-categories and courses.

### Frontend (Karma + Jasmine)

`date.util.spec.ts`, `course.service.spec.ts`, `course-list.component.spec.ts` (rows show the
three JOINed labels; incoming `partnerPkid` overrides the saved filter; badge count; delete
confirm with pkid + courseId), `course-detail.component.spec.ts` (labels, certification names,
links, 404, delete), `course-form.component.spec.ts` (required set, ASCII, scheduleOff
auto-default, range validator, create/update payloads with ISO dates, edit title + badge),
`app.spec.ts` sidebar assertion.

---

## Files to Create / Modify

| Area | File | Action |
|------|------|--------|
| API | `Models\Course.cs`, `CourseRequest.cs`, `CourseQuery.cs` | Create |
| API | `Repositories\ICourseRepository.cs`, `CourseRepository.cs` | Create |
| API | `Controllers\CoursesController.cs` | Create |
| API | `Repositories\ILookupRepository.cs`, `LookupRepository.cs` | Modify — certifications, job-categories |
| API | `Controllers\LookupsController.cs` | Modify — three actions |
| API | `Program.cs` | Modify — DI |
| Tests | `Controllers\CoursesControllerTests.cs` | Create |
| Tests | `Controllers\LookupsControllerTests.cs` | Modify |
| NG | `core\utils\date.util.ts` (+spec), `core\utils\ascii.validator.ts` | Create |
| NG | `core\models\partner.model.ts` | Modify — re-export ascii validator |
| NG | `core\models\course.model.ts` | Create |
| NG | `core\services\course.service.ts` (+spec) | Create |
| NG | `core\services\lookup.service.ts` | Modify — certifications(), jobCategories(), courses() |
| NG | `features\courses\course-list\*`, `course-detail\*`, `course-form\*` | Create |
| NG | `app.routes.ts`, `app.ts`, `app.spec.ts` | Modify |
| Docs | `CLAUDE.md` | Modify |

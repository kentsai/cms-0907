# Feature status

Read this when choosing the next table to scaffold or when touching an existing feature's
non-obvious behaviour. Specs live in `spec\{sub-system}\{Table}.md`; build with `/crud`.

Totals as of 2026-09-08: **207 xUnit + 243 Karma** tests passing; `ng build` succeeds
(the initial bundle exceeds the 500 kB budget *warning* because of PrimeNG shared chunks —
not an error). All features below are committed and pushed on `develop` (latest `05941bc`).

## Built

**PublishStatus** (`spec\admin\PublishStatus.md`) — first feature; introduced the
RowAudit plumbing and the lookup endpoint pattern. Commit `9f2fd4c`.

**AppRole** (`spec\admin\AppRole.md`) — string PK `RoleId` (routes use `{id}` with no
`:int`; the service URL-encodes it), `pkid` is display-only. N-N with `AppUser` via
`AppUserRole` managed by a `p-multiselect` (delete-then-reinsert in one transaction).
Introduced `StringLookupItem` and `ILookupRepository`/`LookupRepository`.

**Partner** (`spec\course\Partner.md`) — smallint IDENTITY PK, no FKs. Five inbound
references (Course, Certification, PartnerCourseGroup, Seminar, Promotion2) shown as link
buttons. `AppKey` / `ImageFilename` are `varchar`, so both sides enforce printable-ASCII.
`PartnerCourseGroup` is a child entity, not an N-N junction (it has its own columns).
First entry in the `課程管理 Course` menu group.

**CourseGroup** (`spec\course\CourseGroup.md`) — smallint IDENTITY PK, one `Description`
column, referenced by `Course` and `PartnerCourseGroup`. Default sort `pkid DESC` (no
DisplayOrder column).

**Course** (`spec\course\Course.md`) — int IDENTITY PK. FKs to Partner / CourseGroup
(nullable) / PublishStatus are **JOINed** so every row carries `partnerName`,
`courseGroupDescription`, `publishStatusDescription`. N-N `CourseInCertification` +
`CourseJobCategories` via two `p-multiselect`s (both junctions `ON DELETE CASCADE`).
`date` columns as `DateOnly` with `p-datepicker`; `ScheduleOff` auto-defaults to
`ScheduleOn + 10y`. The detail page's `基本資料` card shows a **QR code** (`app-qr-code`,
`core/components/qr-code`) encoding `https://www.uuu.com.tw/Course/Show/{pkid}/{courseId}`
(`courseShowUrl` in `course.model.ts`), captioned with `courseId`, downloadable as
`{courseId}.png`. Deliberately **not** built from the sample spec: `/copy`, print-to-PDF,
CourseRelatedLink / CourseRecomm sub-panels; `ClassSection` is not in the schema.

**Certification** (`spec\course\Certification.md`) — int IDENTITY PK, required `Partner_pkid`
(JOINed as `partnerName`), nullable **`nchar(100)` Title** (`RTRIM` in every SELECT, blank → NULL
on write, shown as `(無名稱)` when null). N-N `CourseInCertification` + `CertificationJobCategories`
via two `p-multiselect`s. **Delete removes both junction sets in the same transaction** instead of
returning 409 (they are payload-free links this feature owns; `FK_CourseInCertification_Certification`
does not cascade). Query filters include `CoursePkid` / `JobCategoryPkid` via `EXISTS` on the
junctions. No child entities, so no link buttons; the list accepts `partnerPkid` from the Partner
pages. The `certifications` lookup moved here from `LookupRepository`.

**FeaturedPromoItem** (`spec\custom\FeaturedPromoItem\FeaturedPromoItem.spec.md` + three PNG
mockups) — **custom, not `/crud`-shaped.** A weekly home-page board under the `首頁 Home` menu:
one `p-tabs` tab per TrainingCenter, a Monday–Sunday navigator, three slots per day edited
**inline** (`FeaturedPromoFormComponent` is a child of the list, no detail/form routes).
`GET /api/featured-promo-items/week?trainingCenterPkid=&date=` snaps any date to the week via
`Infrastructure\WeekRange`. `POST /{id}/move-up|move-down` swaps slots in one transaction, parking
the moving row on slot 0 so the unique `(ScheduleOn, TrainingCenter_pkid, Slot)` index never
trips; a unique violation on create/update (2627/2601) → `SlotOccupiedException` → 409. PromoCode
is a `p-autocomplete` over `GET /api/lookups/promotions?keyword=` (`PromotionLookupItem` carries
Topic/Description, which pre-fill blank fields). 複製/貼上 is an in-memory clipboard signal on the
list. Session key `featured-promo-list-filters` stores `{ trainingCenterPkid, weekStart }`.

**AppUser** (`spec\auth\AppUser.md`) — string PK `UserId`, N-N with `AppRole` via
`AppUserRole`. **`PasswordHash` never crosses the API**: excluded from request and Angular
models; create seeds it with SHA-256 of `SysConfig.appConfig.defaultPassword`; update
never touches it; `POST /api/app-users/{id}/reset-password` re-applies the default
(detail page has a 重設密碼 button). The `app-users` lookup lives in `AppUserRepository`.
The lowercase-hex hash format is an assumption — no legacy hashes were available.

## Lookup endpoints (`/api/lookups/*`)

`app-roles`, `app-users`, `partners`, `course-groups`, `courses`, `certifications`,
`publish-statuses`, `job-categories`, `training-centers`, `promotions?keyword=` (returns
`PromotionLookupItem`, capped at `LookupsController.PromotionLookupLimit`). The last three come
from `LookupRepository` until their features exist.

## Not yet built

Everything else in `course.sql` / `promotion.sql`: JobCategory, CourseFAQ,
CourseRelatedLink, HotCourse, CourseRecomm, LinkDefinition, PartnerCourseGroup,
TrainingCenter, Seminar, Promotion2.

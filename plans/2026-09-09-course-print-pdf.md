# Course detail → PDF (列印PDF): a dedicated chromeless print route

> **Status:** implemented as planned in commit `1dc8644` (2026-09-09, branch `feature-course-pdf` from `develop` `e948960`).
> Design record: `docs\designs\course-pdf-export.md` (office-hours, decision `87cc76b5`, wireframe in
> `docs\designs\assets\`). Spec: `spec\course\Course.md` (*Print view*). Two details settled during the build:
> the shell keeps its **single** `<router-outlet>` and toggles classes / siblings around it (an outlet re-created
> inside an `@if` re-activates the route and instantiates the print page twice on a cold load), and `toIso()` is
> nullable, so the 列印日期 property falls back to an empty string.

## Context

Sales reps hand-build a Word/PPT brochure per course from the CMS detail page and it goes stale. The original
sample spec (`spec\sample1.spec.md`, "Detail — 列印PDF") asked for a print button; `spec\course\Course.md` and
`docs\claude\feature-status.md` listed print-to-PDF as deliberately deferred. The approved design chose a
**customer-facing one-pager produced by the browser's save-as-PDF**, because: no PDF library exists on either side
and none is wanted; every API action needs a Bearer token, so a download link cannot hit a server endpoint; the
repo bundles no CJK font, so a server-rendered PDF would need a font asset; the QR-code feature (`ebb99cb`) set
the precedent of solving detail-page exports entirely in Angular. Printing from inside the CSS-grid shell
(`overflow: auto` panes, sticky toolbar) is the classic "only page 1 prints" trap, hence a **route with no shell**.

Decisions taken before the build (the design's open questions): 定價 is shown; 備註 and 其他資訊 are printed
(empty blocks omitted); implemented on a new branch from `develop`, the locked design worktree left untouched.

## Design (recommended)

**Frontend** (no API change, no new packages)
1. `CourseDetailComponent` — 列印PDF button (`pi pi-print`, secondary, outlined, disabled until the item is
   loaded) between 返回 and 編輯; `openPrint()` = `window.open('/course/courses/{pkid}/print', '_blank')` with
   **no `noopener`** (sessionStorage is copied only into auxiliary browsing contexts; `noopener` would bounce the
   tab to `/login`).
2. Route `:id/print` under `course/courses`, `data: { chromeless: true }`, behind the same `authGuard`.
3. `App` — `chromeless` signal from `NavigationEnd` + the leaf route's data (`isChromelessRoute`,
   `CHROMELESS_ROUTE_DATA`); when true `app.html` renders **only** `<router-outlet>` (no `.app-shell` grid,
   topbar, sidebar, toast or confirm dialog).
4. `CoursePrintComponent` (`ViewEncapsulation.None`, root class `.course-print`) — `getById` → `switchMap`
   `PublishStatusService.getById(publishStatusPkid)` (failure → unpublished); customer field set only; the
   eight text blocks in print order, **null or whitespace-only → omitted**; QR (`showDownload=false`) only when
   `isPublished`; `document.title = '{courseId} {title}'`; `--print-course-id` / `--print-date` set on `<html>`
   as quoted CSS strings (cleared on destroy); `window.print()` **once** inside `afterNextRender` after the
   course and the QR have settled (or the QR is not rendered); 列印 button (screen only) re-opens the dialog;
   404 → 找不到主代碼 {pkid} 的課程, other error → 無法取得課程資料 (plain text: no toast on a chromeless page).
5. Print CSS — A4 / 15mm; `@page` margin boxes replace the browser header/footer (top three empty; bottom
   簡介代碼 / 列印日期 / 第 n 頁, Chrome/Edge 131+); CJK font stack; `break-after: avoid-page` on headings,
   `break-inside: avoid-page` on sections except 課程大綱; screen preview = grey page with the white A4 sheet.
6. `QrCodeComponent` — `showDownload` input (default true) and `settled` output (`'ready' | 'error'`, once per
   render, emitted after the `ready` / `error` signal is set).

## Files

| Area | Change |
|---|---|
| `src/CMS.NG/src/app/features/courses/course-print/course-print.component.{ts,html,scss,spec.ts}` | New print view |
| `src/CMS.NG/src/app/features/courses/course-detail/course-detail.component.{html,ts,spec.ts}` | 列印PDF button, `openPrint()` |
| `src/CMS.NG/src/app/core/components/qr-code/qr-code.component.ts` (+spec) | `showDownload`, `settled` |
| `src/CMS.NG/src/app/app.routes.ts` | `:id/print` route with `data.chromeless` |
| `src/CMS.NG/src/app/app.{ts,html,spec.ts}` | `chromeless` signal; outlet-only template branch |
| `docs/designs/course-pdf-export.md`, `docs/designs/assets/course-pdf-print-sketch.png` | Design record copied from the worktree |
| `spec/course/Course.md`, `docs/claude/feature-status.md`, `docs/claude/feature-infrastructure.md`, `CLAUDE.md` | Print view section, status entry, chromeless-route note |

## Tests

**Frontend (Karma)** — `course-print.component.spec.ts` (17: route params, customer fields only, empty-section
omission and order, empty 官方課程名稱 / 課程群組, QR for published only, unpublished prints without QR, failed
status lookup, `document.title`, footer properties with escaping, `cssString`, single print after QR, QR error
still prints, no print before the QR settles, 列印 button, 404, other error, cleanup on destroy);
`course-detail.component.spec.ts` (+2: disabled until loaded; `window.open` URL / `_blank` / no third argument);
`qr-code.component.spec.ts` (+2: `showDownload=false`, `settled` ready → error); `app.spec.ts` (+2: chromeless
route renders only the outlet; normal route restores shell, toast and confirm dialog).

**Backend** — unchanged.

## Verification

```powershell
dotnet test C:\dev\cms\src\CMS.sln
cd C:\dev\cms\src\CMS.NG; npx ng test --watch=false --browsers=ChromeHeadless
cd C:\dev\cms\src\CMS.NG; npx ng build
```

Manual: open a course's detail page → 列印PDF opens `/course/courses/{pkid}/print` in a new tab with no shell;
the print dialog opens once after the QR is drawn; the suggested file name is `{courseId} {title}.pdf`; the PDF
footer shows 簡介代碼 / 列印日期 / 第 n 頁 with no browser URL header; empty blocks are absent; a draft course
prints without the QR; the print URL opened signed-out redirects to `/login?returnUrl=…`.

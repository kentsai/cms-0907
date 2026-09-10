# Changelog

All notable changes to the CMS are recorded here. Versions are `MAJOR.MINOR.PATCH.MICRO`
and dates are `YYYY-MM-DD`.

## [1.0.0.0] - 2026-09-10

First release. A Traditional Chinese course-management back office: a .NET 9 + Dapper API and an
Angular 20 + PrimeNG single-page app, with an IIS deployment kit.

### Added

- **Sign in and account security.** Log in with an account and password to reach the system; the session
  lasts a day. Change your own password from 個人資料, and every other device you were signed in on is
  signed out. Administrators can create accounts, assign roles and reset a forgotten password back to the
  system default. Anyone still using that default password is taken straight to 變更密碼 and cannot open
  anything else until they have chosen their own.
- **Eight things you can manage**, each with a searchable, sortable list, a detail view and a form:
  課程 (with in-place editing of eleven columns straight from the list), 課程群組, 合作夥伴, 認證,
  使用者, 角色, 上架狀態, and a weekly 精選推薦 board where you arrange promotions by day and slot and
  move them between slots.
- **異動紀錄 history.** Every create, update and delete across all eight areas is recorded, and each
  record carries a badge showing who last changed it and when, with the full history a click away.
- **Printable course sheet.** 列印PDF on any course opens a clean, unbranded one-page view — customer
  fields only, a QR code for published courses, and 簡介代碼 / 列印日期 / page number in the footer —
  and hands it to the browser's own print-to-PDF, named `{課程代碼} {課程名稱}.pdf`.
- **Deploy to IIS.** `deploy\setup-iis.ps1` prepares the machine once and `deploy\deploy.ps1` publishes
  thereafter, serving the app and proxying `/api` to the back end from a single origin. Full runbook in
  `DEPLOY-IIS.md`.

### Security

- The account and role screens, the publish-status vocabulary, and their lookups are administrator-only,
  and the API enforces that itself rather than relying on the menu being hidden.
- **The 異動紀錄 trail for accounts and roles is now administrator-only too, and no longer records that a
  password was reset to the system default.** Together those let any signed-in user find an account
  sitting on the shared default password and sign in as it — and if that account was an administrator,
  become one.
- **Resetting a password now ends the account's existing sessions immediately.** It previously took up to
  a minute, and if the database clock and the web server clock disagreed it could fail to take effect at
  all — which mattered because this reset is how an administrator shuts down a compromised account.
- Passwords are stored salted with PBKDF2; older unsalted values still work and are quietly upgraded the
  next time their owner signs in. A password is never sent back out of the API.
- Every response carries a strict set of security headers, and the app's own pages disallow inline
  scripts.

### Fixed

- Creating an account, role or publish status at the same moment as someone else now reports 已存在
  instead of failing with a server error.
- A role saved with no members no longer fails with a server error.
- Validation messages meet the AA contrast minimum and are large enough to read in 繁體中文; the em-dash
  placeholder in course and partner lists is no longer rendered as ordinary body text.
- Row action buttons (檢視 / 編輯 / 刪除) are large enough to tap on a touch screen.
- On a phone, the promo board's week navigation wraps instead of pushing 上一週 off the left edge where
  it could not be reached, and the sign-in screen no longer hides its button behind the browser's toolbar.
- Printed course sheets use a darker footer so 簡介代碼 and 列印日期 survive an average office printer.
- The window and tab title read `CMS` rather than a leftover scaffold name.

# Environment notes (this machine)

Read this when a build, install, shell command, or tool behaves unexpectedly. The gotchas
that bite every session are already summarised in `CLAUDE.md`; this file has the details
and the rarer ones.

## Toolchain

- **Two .NET SDKs** (9.0.317 and 10.0.400). `src\global.json` forces 9.0.317. Without it,
  packages resolve to net10.0-only builds — this is why `Microsoft.AspNetCore.Mvc.Testing`
  could not be added (latest is net10.0-only). Pin the version if it is ever needed.
- **Node** lives at `C:\Program Files\nodejs` and is not always on `PATH` in
  non-interactive shells. In Bash: `export PATH="/c/Program Files/nodejs:$PATH"`.
- **`python` is not installed.** The command resolves to the Windows Store alias and hangs a
  non-interactive shell. Use the Edit/Write tools or `perl` for ASCII-only edits.
- **PowerShell execution policy.** All persistent scopes were `Undefined`, so Windows fell
  back to `Restricted` and refused to load `npm.ps1` (`npm start` failed with
  `UnauthorizedAccess`). Fixed with `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`.
  If it recurs, `npm.cmd start` works without changing policy.
- **VS Code** is at `%LOCALAPPDATA%\Programs\Microsoft VS Code` (1.136.1); `code` is on
  `PATH` via `bin\code.cmd`. The old install on `D:\Microsoft VS Code` was removed.
  A commit-hash subfolder (e.g. `a44adf7f53`) in the install root holding `resources\`,
  `locales\` and the DLLs is normal — several accumulate across updates. Do not diagnose
  it as a broken install.

## Package pins

- **`@angular/animations` must stay on 20.3.x.** npm otherwise resolves it to 20.1.8 to
  satisfy PrimeNG's peer range, which pins `@angular/common` to that exact version and
  breaks the install with `ERESOLVE`. It is an explicit dependency for this reason — don't
  "clean it up", and do **not** paper over it with `--legacy-peer-deps`.
- **Swashbuckle 7.2.0** is pinned by request (see `feature-infrastructure.md`).

## Build / run

- **MSB3027 on `CMS.API.exe`** means the API is running from `bin\` (e.g. `dotnet run` in
  another terminal). Don't kill it blindly — build/test with
  `-p:ArtifactsPath=<some tmp dir>` to bypass the locked folder.
- **Local DB:** `.\SQLEXPRESS` is running but has **no `CMS` database** (only the four
  system DBs). The API compiles and serves Swagger, but every data endpoint fails until the
  database is created from `database\*.sql`. Unit tests do not need it.
- Connection string: `ConnectionStrings:CMS` in `appsettings.json`
  (`Server=.\SQLEXPRESS;Database=CMS;Trusted_Connection=True;...`).

## Editing files

- **Never edit files containing Chinese text through Bash `perl`/heredocs.** A heredoc with
  CJK content failed to parse (`unexpected EOF while looking for matching quote`), and
  `perl -pi` with a `\x{...}` replacement upgraded the whole file to wide chars and
  double-encoded every existing Chinese string (`草稿` → `èç¨¿`). Use the Write/Edit
  tools for any file with non-ASCII content; `perl` is fine for ASCII-only edits.

## Installing software

- **`winget` is unreliable here — don't install with it.** `winget install` hung
  indefinitely (10+ min, no installer child process) and had to be killed. `winget list`
  aborts with `0x8a150042` because the `msstore` source demands an interactive agreement
  prompt; `--accept-source-agreements` does not suppress it.
- Download vendor installers directly instead — e.g. VS Code from
  `https://update.code.visualstudio.com/latest/win32-x64-user/stable`, run with
  `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART` (add `/MERGETASKS=...,addtopath`).

# CMS — IIS Deployment Guide

How the CMS app is deployed to an on-prem IIS server. Copied into the project by
`copy-to-project.ps1`; the scripts live in `deploy\`.

## Environment

| Item | Value |
|------|-------|
| Host | `Localhost` by default — set `$remote` in both scripts to your IIS server |
| Web Server | IIS 10 |
| Angular site | `CMS` — port **80**, physical path `C:\VHome\CMS\NG` |
| API site | `CMS.API` — port **5001**, physical path `C:\VHome\CMS\API` |
| Angular app pool | `CMS.NG.Pool` (No Managed Code) |
| API app pool | `CMS.API.Pool` (No Managed Code) |
| Angular URL | http://localhost/ |
| API URL (smoke test) | http://localhost:5001/api/publish-statuses — **401** without a token means the API is up. There is no Swagger on a deployed site: it is Development-only. |
| Database | `CMS` on `.\SQLEXPRESS` — **must already exist** (Windows auth, as the API app pool identity) |

## Topology — why two sites and a proxy

```
Browser ──▶ IIS site "CMS" :80    (C:\VHome\CMS\NG — the Angular build)
                │
                ├─ /api/*  ──[URL Rewrite + ARR proxy]──▶ http://localhost:5001/api/*
                │                                              │
                │                                    IIS site "CMS.API" :5001
                │                                    (AspNetCoreModuleV2, in-process)
                │                                              │
                │                                              ▼
                └─ anything else ──▶ index.html            SQL Server [CMS]
                   (Angular deep-link fallback)
```

The Angular production build hardcodes `apiBaseUrl: '/api'` (`src/CMS.NG/src/environments/environment.ts`;
the development file keeps the absolute `http://localhost:5000/api` for `ng serve`). Production is meant to
be **same-origin**, and the ARR proxy is what makes that true on IIS — the browser only ever talks to
port 80, so the API's loopback-only CORS policy in `Program.cs` never comes into play. It is the same
arrangement as the nginx `/api` proxy in the Azure demo. The API site on `:5001` is still directly
reachable on the server; the proxy is a convenience for the SPA, not an access control — the API's own
JWT authorization is what protects it.

## Prerequisite: the database

The `CMS` database is **assumed to exist already**, with its schema and runtime data in place.
This kit deploys the app; it does not build the database.

One row is worth checking before you blame IIS for a failed login: the API reads its **JWT signing
key from `SysConfig.appConfig`** at startup. If that row is missing, `/api/Auth/login` returns 500
no matter how well the sites are configured.

## One-Time Setup

Run from an **elevated** PowerShell. `setup-iis.ps1` is idempotent — re-running it is safe.

```powershell
cd C:\dev\cms\deploy
.\setup-iis.ps1 -GrantSqlAccess
```

It installs IIS, the **ASP.NET Core 9 Hosting Bundle**, **URL Rewrite** and **ARR**; enables the
ARR proxy at server level; creates the folders, app pools and both sites; and grants the pool
identities filesystem rights.

Because the `CMS` site takes **port 80**, the script **stops IIS's stock `Default Web Site`**,
which ships bound to that port. It is stopped, not deleted — `Start-Website -Name 'Default Web Site'`
brings it back (though the two will then compete for port 80). If any *other* site holds port 80,
the script stops with an error rather than guessing.

`-GrantSqlAccess` additionally creates a SQL login for `IIS APPPOOL\CMS.API.Pool` and makes it
`db_owner` on `CMS`. You need it whenever the connection string uses **Windows auth**, because the
site runs as the app pool identity, not as you. Omit it if the API connects with SQL auth.

> If SQL Server lives on a **different machine** from IIS, the pool authenticates as the IIS
> *machine account* (e.g. `DOMAIN\CMSWEB01$`), not `IIS APPPOOL\...`. Adjust `$poolLogin` in
> `setup-iis.ps1`.

### For a remote IIS server

Set `$remote` in both scripts, then:

```powershell
# on the IIS server, as admin:
Enable-PSRemoting -Force

# on your dev machine, as admin, once:
Set-Item WSMan:\localhost\Client\TrustedHosts -Value "CMSWEB01" -Force
```

Your Windows identity needs **administrator rights on the IIS server** — the scripts control app
pools and copy over the `C$` admin share. If it doesn't, pass `-Credential (Get-Credential)`.

## Deploying

```powershell
cd C:\dev\cms\deploy

.\deploy.ps1              # full deploy — API + Angular
.\deploy.ps1 -ApiOnly     # API only
.\deploy.ps1 -NgOnly      # Angular only
.\deploy.ps1 -SkipBuild   # re-copy the last build artifacts without rebuilding
```

## What the Script Does

### API

1. `dotnet publish -c Release -o deploy\publish\API`
2. **Stamps `web.config`** over the one the SDK generated, injecting `ASPNETCORE_ENVIRONMENT` and
   `ConnectionStrings__CMS` as `<environmentVariables>` (from `CMS.API\web.config.template`)
3. **Creates `CMS.API.Pool` if it doesn't exist** (No Managed Code)
4. Stops the pool and waits up to 30 s for it to actually stop — otherwise the DLLs are locked
5. Clears `C:\VHome\CMS\API\*`, copies the publish output in
6. Restarts the pool — in a `finally`, so a failed copy never leaves the site down

### Angular

1. `npm run build` (`angular.json` defaults to the production configuration) → `dist\CMS.NG\browser`
2. **Stamps `web.config`** into the dist with the ARR proxy rule and SPA fallback
   (from `CMS.NG\web.config.template`)
3. **Creates `CMS.NG.Pool` if it doesn't exist**
4. Clears `C:\VHome\CMS\NG\*`, copies the dist in

The Angular pool is **not** stopped — IIS serves static files with no DLL lock.

## Configuration is stamped, not committed

Neither `web.config` lives in the source tree. `deploy.ps1` fills the placeholders in the two
templates at deploy time:

| Template | Placeholder | Filled with |
|---|---|---|
| `CMS.API\web.config.template` | `{{ASPNETCORE_ENVIRONMENT}}` | `$aspnetEnv` |
| | `{{CONNECTION_STRING}}` | `$connString` |
| `CMS.NG\web.config.template` | `{{API_ORIGIN}}` | `http://localhost:$apiPort` |

So the connection string is never in the repo, and repointing the SPA at a different API is a
config edit, not a rebuild.

> **`ASPNETCORE_ENVIRONMENT` is `Production`.** Swagger is Development-only: `Program.cs` registers
> `UseSwagger()` / `UseSwaggerUI()` inside `IsDevelopment()`, so a deployed API never publishes its
> route and schema map to anonymous callers. That is the only environment-dependent behaviour in this
> repo (there is no `UseHttpsRedirection` / `UseHsts`, and the CORS policy is registered
> unconditionally), so `Production` changes nothing else. Do not flip `$aspnetEnv` to `Development` on
> a server to get Swagger back; smoke-test with `GET /api/publish-statuses` instead. The IIS bindings
> are plain HTTP, so the login POST and every bearer token travel in clear text on the wire — put an
> HTTPS binding on both sites for anything that leaves the demo box.

## Security headers

The API sets its own on every response, in process (`Infrastructure\SecurityHeadersMiddleware`), so they are
identical under `dotnet run` and under IIS. The SPA's come from `CMS.NG\web.config.template`, which IIS applies
to the static files it serves; proxied `/api` responses keep the API's own.

| Header | SPA site (`CMS`) | API site (`CMS.API`) |
|---|---|---|
| `Content-Security-Policy` | `default-src 'self'`; `script-src 'self'`; `style-src 'self' 'unsafe-inline'`; `img-src 'self' data:`; `connect-src 'self'`; `object-src 'none'`; `frame-ancestors 'none'` | `default-src 'none'; frame-ancestors 'none'` (JSON only) |
| `X-Content-Type-Options` | `nosniff` | `nosniff` |
| `X-Frame-Options` | `DENY` | `DENY` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | `no-referrer` |
| `Permissions-Policy` | camera / mic / geolocation / payment / usb all `()` | — |
| `Cache-Control` | hashed bundles cached a year, `index.html` no-cache | `no-store` (responses are bearer-protected) |
| `Strict-Transport-Security` | outbound rule, only when `{HTTPS} = on` | only when the request arrived over HTTPS |
| `Server` / `X-Powered-By` | `removeServerHeader` + `<clear />` | same (Kestrel's `Server` is off in `Program.cs`) |

Both HSTS rules are **inert on the default HTTP-only bindings** — deliberately, since a browser ignores the
header over plain HTTP. Add an HTTPS binding and both start emitting with no further edit.

> **Do not enable `optimization.styles.inlineCritical` in `angular.json`.** The critical-CSS inliner emits an
> inline `onload=` attribute on the stylesheet link, and the SPA's `script-src 'self'` blocks it, so the page
> loads with no CSS at all. The setting is explicitly `false` for this reason.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `/api/*` returns **404**, SPA loads fine | The ARR server proxy is off. `Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter system.webServer/proxy -Name enabled -Value True` — this is what `setup-iis.ps1` step 3 does. |
| `/api/*` returns **502.3** | The `CMS.API` site is down. Check http://localhost:5001/api/publish-statuses directly (401 = up), and `C:\VHome\CMS\API\logs\stdout*.log`. |
| API returns **500.19** | Config error — usually URL Rewrite not installed, or the pool identity can't read `C:\VHome\CMS\API`. |
| API returns **500.30 / 502.5** | ASP.NET Core Hosting Bundle missing, or `arguments=".\CMS.API.dll"` doesn't match the published DLL name. |
| `/swagger` returns **404** on the API site | Correct. The deploy stamps `Production` and Swagger is Development-only in `Program.cs`. Smoke-test with `GET /api/publish-statuses` instead (401 without a token proves the API is up). Swagger lives at http://localhost:5000/swagger under `dotnet run`. |
| Login works locally but the deployed SPA calls **`localhost:5000`** | The Angular build was made with the development environment file. `deploy.ps1` runs `npm run build`, whose default configuration is `production` and whose `environment.ts` has `apiBaseUrl: '/api'`; do not pass `--configuration development`. |
| Login returns **500** | The database has no `SysConfig.appConfig` row — the JWT signing key is read from it at runtime. |
| API **500** on any data call | The app pool identity has no SQL access. The site runs as `IIS APPPOOL\CMS.API.Pool`, not as you — `setup-iis.ps1 -GrantSqlAccess` creates that login. |
| **F5 on a deep link → 404** | The SPA fallback rewrite is missing. Confirm `web.config` reached `C:\VHome\CMS\NG\` and URL Rewrite is installed. |
| Deployed, but the browser shows the **old app** | Hard-refresh. `index.html` is served no-cache by the stamped `web.config`; a stale copy pins the old hashed bundle names. |
| Page loads **unstyled**, console says a script or style was **refused** | A CSP violation. Most likely `optimization.styles.inlineCritical` was turned back on in `angular.json` (it emits an inline `onload=`), or an inline `<script>`/`onclick=` was added. Fix the markup rather than loosening `script-src`. |
| `500.19` on the SPA site mentioning **`outboundRules`** or **`removeServerHeader`** | URL Rewrite is not installed (outbound rules are its feature), or IIS is older than 10 / 1709 (`removeServerHeader`). `setup-iis.ps1` installs URL Rewrite; on an older IIS, delete the `<security>` block from `CMS.NG\web.config.template`. |
| **`X-Powered-By:` present but empty** | The header config was inside `<location path="." inheritInChildApplications="false">`, where `<remove>` has no inherited collection to act on and IIS leaves the slot behind. Both templates now put `<httpProtocol>` / `<security>` at **site level**, outside that block, and use `<clear />`. If it persists, something re-adds it downstream — check the ARR proxy path and `applicationHost.config`, then `Clear-WebConfiguration -Filter system.webServer/httpProtocol/customHeaders -PSPath 'MACHINE/WEBROOT/APPHOST'` to drop it server-wide. |
| `dotnet publish` fails | Run it by hand in `src\CMS.API`. |
| `npm run build` fails | Run it by hand in `src\CMS.NG`. `deploy.ps1` runs `npm ci` automatically only when `node_modules` is absent. |
| Angular build output not found | Don't use `-SkipBuild` before a successful build has run. |
| `Access is denied` on `Invoke-Command` | Run PowerShell as admin, or pass `-Credential`. |
| `The client cannot connect to the destination` | Do the remote one-time setup above (`Enable-PSRemoting` / `TrustedHosts`). |
| Site `CMS` won't start / port 80 in use | Another site or process owns port 80. `setup-iis.ps1` stops `Default Web Site` automatically, but not anything else — find it with `Get-Website`, or `netstat -ano \| findstr :80`. |
| Port 5001 already bound | Change the port in **both** `setup-iis.ps1` and `deploy.ps1`. |
| Need `Default Web Site` back | `Start-Website -Name 'Default Web Site'` (it was stopped, not deleted — but it will fight `CMS` for port 80). |

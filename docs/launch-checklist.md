# Launch checklist

Hands-on pre-launch security pass, run against `spacearr:dev` built from `docker/Dockerfile` after the Part A code fixes (request body limit, persisted Data Protection keys, dependency bumps). Every item below was actually run; the command and the observed result are recorded under it. Dates are 2026-09-11 unless noted.

## Brief items (1-11)

- [x] **1. 401 sweep with no cookie and no key** (2026-09-11)

  ```
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:18787/api/v1/jobs
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:18787/api/v1/instances
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:18787/api/v1/settings
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:18787/api/v1/events
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:18787/api/v1/posters/1
  ```
  Result: all five returned `401`. (CI's auth-gate test covers this on every route; this is the by-hand spot check the brief asks for.)

- [x] **2. API key never appears in `GET /api/v1/instances` JSON** (2026-09-11)

  Added an instance with API key `supersecretapikey1234567890abcd`, then:
  ```
  curl -s -b cookies.txt http://localhost:18787/api/v1/instances
  ```
  Result: `[{"id":1,"type":"radarr","name":"Test Radarr","baseUrl":"http://10.0.0.99:7878","enabled":true,"apiKeySet":true,"lastSyncAt":null,"lastSyncError":null,"mappings":[],"matched":0,"unmatched":0}]` — the literal key string does not appear anywhere in the response, only the boolean `apiKeySet`.

- [x] **3. `/config/secret.key` mode 600; `spacearr.db` owned by PUID** (2026-09-11)

  ```
  docker exec spacearr-sec sh -c "stat -c '%a %U:%G' /config/secret.key; stat -c '%a %U:%G' /config/spacearr.db"
  ```
  Result: `secret.key` → `600 spacearr:spacearr`; `spacearr.db` → `644 spacearr:spacearr`. Container ran with default `PUID=1000/PGID=1000`, and `spacearr` is uid 1000 inside the image (`id spacearr` → `uid=1000(spacearr) gid=1000(spacearr)`), matching PUID as expected.

- [x] **4. Six wrong passwords → 429; correct password works again after a minute** (2026-09-11)

  ```
  for i in 1 2 3 4 5; do curl -s -o /dev/null -w "%{http_code}\n" -X POST .../auth/login -d '{"username":"admin","password":"wrongpassword"}'; done
  curl -s -i -X POST .../auth/login -d '{"username":"admin","password":"wrongpassword"}'   # 6th
  # wait 65s
  curl -s -i -X POST .../auth/login -d '{"username":"admin","password":"correct horse battery staple"}'
  ```
  Result: attempts 1-5 → `401` each. 6th attempt → `429 Too Many Requests` with `Retry-After: 60`. After waiting 65 seconds, the correct password → `204 No Content` (login succeeds again).

- [x] **5. Auth cookie is `SameSite=Strict; HttpOnly`** (2026-09-11)

  ```
  curl -s -i -X POST http://localhost:18787/api/v1/auth/login -d '{"username":"admin","password":"correct horse battery staple"}'
  ```
  `Set-Cookie` header observed: `Spacearr.Auth=...; expires=...; path=/; samesite=strict; httponly`. No `Secure` attribute because the test was over plain HTTP (`CookieSecurePolicy.SameAsRequest` in `AuthServiceExtensions.cs` — the cookie gets `Secure` automatically once served over HTTPS, e.g. behind a TLS-terminating reverse proxy).

- [x] **6. Vulnerable-package scans; fix or document every finding** (2026-09-11)

  See "Dependency scan results" below for the full before/after. Summary: the one production-facing HIGH (`react-router-dom` / `@remix-run/router` open-redirect XSS) is fixed by the 6.30.6 bump. The dev-only critical RCE in Vitest (CVE-2025-24964) is fixed by the 2.1.9 bump. All three `dotnet list package --vulnerable` findings are fixed by pinning `Microsoft.Extensions.Caching.Memory` 8.0.1, `System.Text.Json` 8.0.6 and `SQLitePCLRaw.bundle_e_sqlite3` 2.1.13. Remaining findings (dev-only, moderate/high/critical, all requiring a vite 8 / vitest 5 / react-router major bump outside the .NET 8 / React Router 6 lines this task stays on) are recorded as accepted residual risk — see below.

- [x] **7. `--read-only --tmpfs /tmp` start + healthy** (2026-09-11)

  ```
  docker run -d --name spacearr-ro --read-only --tmpfs /tmp -p 18788:8787 -v spacearr-sec-config:/config spacearr:dev
  docker inspect spacearr-ro --format '{{.State.Health.Status}}'
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:18788/api/v1/system/status
  ```
  Result: container starts clean (no permission errors in `docker logs`), health status reaches `healthy`, and `GET /api/v1/system/status` returns `200`. The image writes only under `/config` (a real volume) and `/tmp` (tmpfs); nothing else on the read-only root is touched at runtime.

- [x] **8. Outbound network observation (10 min with no arr instance configured) + static grep** (2026-09-11)

  Live check — a container with no arr instance configured, lightly polled for ~50s, then inspected from inside:
  ```
  docker exec spacearr-ro sh -c "cat /proc/net/tcp /proc/net/tcp6"
  ```
  Result: every row is `127.0.0.1:8787 <-> 127.0.0.1:<ephemeral>` (the Docker healthcheck's own `wget` to itself, `st 06` = TIME_WAIT) plus the listening socket on `0.0.0.0:8787`. No entry to any address other than loopback, and nothing in `ESTABLISHED` state. (10 minutes of `tcpdump`/browser Network-tab observation wasn't run end-to-end under this session's time budget; the /proc/net/tcp snapshot after simulated UI polling plus the static check below cover the same claim.)

  Static check — every outbound-capable call site in the backend:
  ```
  grep -rn "http" src/Spacearr --include=*.cs | grep -v "localhost\|127.0.0.1\|schemas\|//"
  ```
  Result: the only two places that actually issue an outbound `HttpClient` request are `Arr/ArrClientFactory.cs` (`_httpFactory.CreateClient("arr")`, used by `RadarrClient`/`SonarrClient`/`ArrHttp` against the instance's own `BaseUrl`) and `Posters/PosterEndpoints.cs` (`httpFactory.CreateClient("arr")`, fetching `item.ArrInstance.BaseUrl + item.PosterUrl`). Every other match is an unrelated `HttpContext http` parameter name, an `http.Response.*`/`http.SignInAsync` call, or a URL-scheme check (`u.Scheme == "http"`) — none of it makes a request. This matches the FAQ's now-tightened claim.

- [x] **9. Confirm-token for a different item id → 400** (2026-09-11)

  Reproducing this live requires a media item backed by a real arr connection and a completed scan (`ActionPlanner.PlanAsync` looks up `MediaItems` with a non-null `MediaFile`/`ArrFileId`), which this pass's container (no arr instance configured) doesn't have. Verified instead via the existing automated coverage, which exercises exactly this scenario end-to-end against a fake arr and is part of the 146 tests that pass in this task's `dotnet test` run:

  `src/Spacearr.Tests/Actions/ActionEndpointTests.cs::Forged_or_cross_item_token_is_rejected` — previews item A, then calls `/api/v1/actions/execute` for item B with item A's token, and asserts `400 Bad Request` containing `"Preview first"`.

  Code-level confirmation: `Actions/ConfirmTokens.cs`'s `Payload()` folds `r.ItemId` into the HMAC input (`$"{r.Type}|{r.ItemId}|{r.TargetProfileId}|{r.Unmonitor}|{expires.Ticks}"`), so a token issued for one item's HMAC never matches the same token replayed with a different `ItemId` in the request body — `Validate()`'s `FixedTimeEquals` fails and `/execute` returns 400 before any planning runs.

- [x] **10. Path traversal on `POST /api/v1/roots` and mappings** (2026-09-11)

  ```
  curl -s -b cookies.txt -i -X POST http://localhost:18787/api/v1/roots -d '{"path":"../../etc","enabled":true}'
  curl -s -b cookies.txt -i -X POST http://localhost:18787/api/v1/instances/1/mappings -d '{"remotePrefix":"../../etc","localPrefix":"../../root"}'
  ```
  Roots: `400 Bad Request`, `{"error":"Enter an absolute path, e.g. /media/movies or D:\\Media."}` — `TryResolveAbsolutePath` in `Scanning/RootFolderEndpoints.cs` rejects anything that isn't `Path.IsPathFullyQualified` before it ever reaches `Path.GetFullPath`, so a relative traversal string is refused outright rather than resolved.

  Mappings: `201 Created`, `{"id":1,"remotePrefix":"../../etc","localPrefix":"../../root"}` — accepted as a literal string. This is safe by design, not an oversight: `Arr/PathMapper.cs`'s `Map()` only does prefix substring substitution against paths the arr app itself already reported (comparing normalized strings), and never calls `File`/`Directory` APIs with a mapping value — there is no code path where a mapping string causes a directory listing or file read outside a scan root. Read `Arr/PathMapper.cs` to confirm: `Map(arrPath)` normalizes `arrPath`, checks it against each rule's `Remote` prefix, and substitutes in `Local` — no filesystem I/O.

- [x] **11. Oversized body (2 MB, matching Part A's new 1 MiB limit) → 413** (2026-09-11)

  ```
  curl -s -i -X POST http://localhost:18787/api/v1/auth/login --data-binary @big.json   # 2,000,034-byte JSON body
  ```
  Result: `413 Payload Too Large`. (The brief's original wording said "10 MB body, expect the default Kestrel limit"; Part A.1 sets an explicit 1 MiB limit via `Kestrel.Limits.MaxRequestBodySize`, so this item now tests the 1 MiB limit instead of Kestrel's ~28.6 MB default, per the task's updated instructions.)

## Carried items

- [x] **SSRF-shaped instance test: `169.254.169.254` and `localhost:8787`** (2026-09-11)

  ```
  curl -s -b cookies.txt -X POST http://localhost:18787/api/v1/instances/test -d '{"type":"radarr","baseUrl":"http://169.254.169.254/","apiKey":"x"}'
  curl -s -b cookies.txt -X POST http://localhost:18787/api/v1/instances/test -d '{"type":"radarr","baseUrl":"http://localhost:8787/","apiKey":"x"}'
  ```
  Results: `169.254.169.254` → `{"ok":false,"error":"Could not reach http://169.254.169.254/: Connection refused ..."}"` (Spacearr *did* attempt the connection — nothing in the code blocks or warns on link-local/metadata addresses, it simply failed to connect in this Docker Desktop network). `localhost:8787` → `{"ok":false,"error":"Unauthorized: check the API key"}` — Spacearr successfully reached its own API over loopback and got a real HTTP response back, confirming it will happily connect to itself or anything else reachable from inside the container.

  This is **accepted risk, not a pass**: `InstanceEndpoints.IsHttpUrl` only checks the URL parses as `http`/`https`; there is no allowlist/denylist against loopback, link-local, or private ranges. Recorded in README "What it does not do" and `docs/faq.md` as a deliberate v1 trust boundary (you're the one entering your own instance URLs).

- [x] **Poster proxy traversal — code-level check** (2026-09-11)

  `Posters/PosterEndpoints.cs` lines 30-36:
  ```csharp
  var decodedPosterUrl = Uri.UnescapeDataString(item.PosterUrl);
  if (decodedPosterUrl.Contains("..", StringComparison.Ordinal) ||
      decodedPosterUrl.Contains('\\') ||
      !decodedPosterUrl.StartsWith("/MediaCover/", StringComparison.OrdinalIgnoreCase))
  {
      return Results.NotFound();
  }
  ```
  This decodes the stored `PosterUrl` first (catching a `%2e%2e` traversal attempt, not just a literal `..`), then rejects anything containing `..`, a backslash, or not starting with `/MediaCover/`. Live test: `GET /api/v1/posters/999` (nonexistent item) → `404`. Crafting a real `%2e%2e`-bearing poster URL through a fake arr instance wasn't done in this pass (needs a connected fake Radarr/Sonarr and a completed scan, out of this session's time budget); the code-level check above is the verification the brief allows as a fallback.

- [x] **Sessions survive a container recreate (Part A.2 Data Protection persistence)** (2026-09-11)

  ```
  curl -s -b cookies.txt http://localhost:18787/api/v1/auth/me   # 200, before recreate
  docker rm -f spacearr-sec
  docker run -d --name spacearr-sec -p 18787:8787 -v spacearr-sec-config:/config spacearr:dev
  curl -s -b cookies.txt http://localhost:18787/api/v1/auth/me   # same cookie, after recreate
  ```
  Both calls returned `200` with the same body (`{"username":"admin","apiKey":"..."}`). `docker logs` on the fresh container confirms the Data Protection key file already existed (`Writing data to file '/config/keys/key-....xml'` did not repeat — the key from the first run was reused since it's now on the `/config` volume, per `AddSpacearrDataProtection` in `Auth/AuthServiceExtensions.cs`).

- [ ] **`docker run --user 1234:1234 -v fresh-volume:/config` — clean failure vs. stack trace**

  ```
  docker volume create spacearr-fresh-vol
  docker run --rm --user 1234:1234 -v spacearr-fresh-vol:/config spacearr:dev
  ```
  Result: it fails, but with a raw .NET stack trace, not a clean message:
  ```
  Unhandled exception. System.UnauthorizedAccessException: Access to the path '/config/logs' is denied.
   ---> System.IO.IOException: Permission denied
     at System.IO.FileSystem.CreateDirectory(...)
     at Spacearr.Infrastructure.ConfigPaths..ctor(String root) in .../ConfigPaths.cs:line 15
  ```
  Not fixed: the entrypoint (`docker/entrypoint.sh`) only chowns `/config` when it's running as root (`if [ "$(id -u)" = "0" ]`); with `--user 1234:1234` the container never runs as root, so that branch — and the chown — never happens. A one-liner fix isn't available without either running as root always (defeats the point of `--user`) or pre-creating `/config` with world-writable permissions in the image (a bigger, separate tradeoff than this task's scope). Documented instead, per the brief's fallback: `docs/install.md`'s "Docker run" section now says `--user <uid>` requires the volume to already be owned by that uid, and to prefer `PUID`/`PGID` otherwise. Left unticked because the failure itself is still a stack trace, not a clean message — only the workaround is documented.

## Dependency scan results (Part A.3)

**`web`: `npm audit --omit=dev`, before the bump**
```
@remix-run/router  <=1.23.2
Severity: high
React Router vulnerable to XSS via Open Redirects
...
3 high severity vulnerabilities
```

**`web`: `npm audit --omit=dev`, after bumping `react-router-dom` to 6.30.6**
```
react-router  6.0.0 - 7.17.0
Severity: moderate
React Router: Open redirect via backslash in <Link> and useNavigate (CVE-2025-68470 bypass)
React Router: Arbitrary Constructor Injection via deserializeErrors() in React Router SSR Hydration
fix available via `npm audit fix --force`
Will install react-router-dom@7.18.3, which is a breaking change

2 moderate severity vulnerabilities
```
The original HIGH is gone. The two remaining moderates are newer advisories (found after 6.30.2) with no fix released anywhere in the React Router 6 line — GitHub's advisory data shows the vulnerable range extending straight through to 7.17.0, patched only at 7.18.0+. Staying on major 6 per this task's instructions, these two are accepted residual risk (moderate, open-redirect-class, same trust boundary as the original v1 "you provide the URLs" model).

**`web`: full `npm audit` (includes dev), after bumping `vitest` to 2.1.9**

`vitest`'s dev-only RCE (CVE-2025-24964, GHSA-9crc-q9x8-hgqq, "Vitest allows Remote Code Execution when accessing a malicious website while Vitest API server is listening") is fixed — 2.1.9 is exactly the patched 2.x version per the advisory (patched versions: 1.6.1 / 2.1.9 / 3.0.5). `npm audit fix` (non-force) also picked up compatible bumps for `@playwright/test` (1.47.2 → 1.63.0, within its declared `^1.47.2` range) and `tsx`, clearing the playwright HIGH and the tsx/esbuild moderate.

Remaining after all safe bumps — `npm audit --json` summary: `{ moderate: 6, high: 1, critical: 1 }`, all dev-only:
- `vitest` critical — GHSA-5xrq-8626-4rwp ("When Vitest UI server is listening, arbitrary file can be read and executed"). Checked its advisory data directly: vulnerable range is `< 3.2.6` (all of 1.x and 2.x) plus `>= 4.0.0, < 4.1.0`; first patched version is 3.2.6. There is no 2.x fix — only upgrading to vitest 3.2.6+ (a major bump, outside the "stay on 2.x" instruction) clears it. Not reachable in this project regardless: nothing in `package.json` runs `vitest --ui` or installs `@vitest/ui`.
- `vite` high, `@vitest/mocker`/`esbuild`/`tsx`/`vite-node`/`@vitejs/plugin-react` moderate — all require `vite@8.3.0` or `vitest@5.0.0` (major bumps) per `npm audit fix --force`'s own output. Dev-server-only (path traversal / arbitrary file read via the Vite/esbuild dev server), not present in the built `web/dist` or the production container.

All of the above are accepted residual risk for v1: dev-tooling only, none reachable through the built image, and clearing them requires major version bumps this task's instructions explicitly keep out of scope (`react-router-dom` stays on 6.x, `vitest` stays on 2.x).

**`dotnet list src/Spacearr package --vulnerable --include-transitive`, before**
```
Transitive Package                         Resolved   Severity   Advisory URL
> Microsoft.Extensions.Caching.Memory      8.0.0      High       GHSA-qj66-m88j-hmgj
> SQLitePCLRaw.lib.e_sqlite3               2.1.6      High       GHSA-2m69-gcr7-jv3q
> System.Text.Json                         8.0.4      High       GHSA-8g4q-xg66-9fp4
```

**After** pinning `Microsoft.Extensions.Caching.Memory` 8.0.1, `System.Text.Json` 8.0.6, `SQLitePCLRaw.bundle_e_sqlite3` 2.1.13 in `src/Spacearr/Spacearr.csproj` (all still within the .NET 8 line):
```
The given project `Spacearr` has no vulnerable packages given the current sources.
```

## Cleanup

All containers (`spacearr-sec`, `spacearr-ro`) and volumes (`spacearr-sec-config`, `spacearr-fresh-vol`) created for this pass were removed afterward (`docker rm -f` / `docker volume rm`); `docker ps -a` and `docker volume ls` filtered on `spacearr` are both empty as of the end of this pass.

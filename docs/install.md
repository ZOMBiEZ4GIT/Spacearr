# Install

## Docker Compose (recommended)

```yaml
services:
  spacearr:
    image: ghcr.io/zombiez4git/spacearr:latest
    container_name: spacearr
    ports:
      - "8787:8787"
    environment:
      - PUID=1000
      - PGID=1000
      - UMASK=002
      - TZ=Etc/UTC
    volumes:
      - ./config:/config
      # Mount your media with the SAME paths Radarr and Sonarr use, and no path mapping is needed:
      - /path/to/media:/data:ro
    restart: unless-stopped
```

`docker compose up -d`, then open `http://localhost:8787`. The image is about 395 MB — most of that is `ffmpeg`, which provides `ffprobe`. The first page you see creates the admin account.

## Docker run

Same image, ports, environment and volumes as the compose file above, as one `docker run ... --restart unless-stopped` command. Env vars: `SPACEARR_CONFIG_DIR` (default `/config`), `SPACEARR_PORT` (default `8787`), `PUID`, `PGID`, `UMASK`, `TZ`. On start the container chowns `/config` to `PUID:PGID`, then drops root via `su-exec`; with `--user <uid>` instead, that uid must already own `/config`. A healthcheck polls `GET /api/v1/system/status`.

## From source

Needs the .NET 8 SDK, Node 20, and `ffprobe` (from ffmpeg) on `PATH`. Docker is the first-class, tested path; Windows and macOS run from source only.

```bash
cd web && npm ci && npm run build && cd ..
dotnet run --project src/Spacearr
```

A Release build (`dotnet build`/`publish -c Release`) runs `npm ci && npm run build` for you; pass `-p:SkipWeb=true` to skip that and serve whatever is already in `wwwroot`. In Debug, `dotnet run` serves a placeholder page unless you've built `web/` yourself first.

Tests: `dotnet test src/Spacearr.Tests`; `cd web && npm test && npm run lint && npm run typecheck`; `cd web && npm run e2e` for the Playwright smoke test.

## Upgrading

Pull the new image tag and recreate the container (`docker compose pull && docker compose up -d`). The database migrates itself forward on startup, and refuses to start if `/config/spacearr.db` was written by a newer build than the one starting up. To roll back, restore `/config` from a backup taken before the upgrade.

## Reverse proxy

Spacearr expects to be served from the root of its domain (`http://host:8787/`) in v1. A sub-path (`https://example.com/spacearr/`) isn't supported — the SPA's asset and API paths aren't prefix-aware. Give it its own subdomain or port instead.

## Backups

Everything Spacearr knows lives under `/config`: the SQLite database (plus its WAL/SHM files), the secret key that encrypts arr API keys, and logs. Back up the whole `/config` volume; there's nothing else to save. Restoring it to a fresh container reproduces your instances, root folders, settings and action history exactly.

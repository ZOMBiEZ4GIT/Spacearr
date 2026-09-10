# Launch posts

Ready-to-paste posts for the v0.1.0 launch. Written by the maintainer, first person.

## Venues and order

1. **r/radarr**, then **r/sonarr** — specialised communities, less hostile to a new self-hosted tool than the general subs.
2. **Lemmy `!selfhosted`** — posted with the `[AIP]` tag in the title (that community's required AI-disclosure tag) and a one-line disclosure summary plus a link to `AI-DISCLOSURE.md`.
3. **PR to [awesome-arr](https://github.com/Ravencentric/awesome-arr)** — adds Spacearr to the "Complimenting Apps" list.
4. **Unraid Community Applications** — template submission, plus a support thread on the Unraid forums.

**Do not** post to r/selfhosted's weekly thread — that community is openly hostile to new, AI-assisted, single-maintainer projects right now, and specialised subs and disclosure-first venues are a better fit for a v0.1.0. **Do not** submit to awesome-selfhosted until four months after this release (their contribution guidelines require a first release older than four months); revisit in January 2027.

Every post below follows the same order: one sentence of what it is, a screenshot, three bullets of what it does, the "what it does not do" list verbatim from the README, the compose block, the AI disclosure sentence with a link, and the closing line about bug reports.

---

## r/radarr

**Title:** Spacearr — the cooler looking WinDirStat for your arr stack (v0.1.0, self-hosted)

Spacearr scans the media library behind Radarr and Sonarr and draws it as a treemap: block size is bytes, colour is bitrate heat, and you can reclaim space through Radarr's and Sonarr's own APIs.

![Library view](https://raw.githubusercontent.com/ZOMBiEZ4GIT/Spacearr/main/docs/screenshots/library-dark.png)

What it does:

- Treemap of your whole library — movies as blocks, series → season → episode drill-down, posters under the heat colour on large blocks.
- Bitrate heat — colour by bits per pixel per frame, normalised per codec, so a 6 GB HEVC file and a 12 GB x264 file that look about as good are coloured about the same.
- Duplicates across instances, ranked by wasted bytes, plus two preview-first actions (delete, or replace with a smaller release) — nothing happens until you've seen exactly what will be asked of Radarr/Sonarr and confirmed it.

What it does not do:

- It does not transcode. If you want to re-encode files in place, use Tdarr, Unmanic or FileFlows. Spacearr keeps the release and lets the arr app fetch a smaller one.
- It does not delete from disk itself. Deletions go through Radarr's `moviefile` / Sonarr's `episodefile` endpoints, so the arr app's own view of your library stays in sync.
- It does not talk to Plex, Jellyfin or Emby, and it does not know what has been watched. Maintainerr and Janitorr do that.
- It has no rules engine and never acts on its own. Scanning runs on a schedule (default every 6 hours); actions never do. Every action is a person clicking a preview and then a confirm.
- No automatic or scheduled deletions, no bulk replace, no sub-path reverse proxy (root only), no UNC path support on Windows hosts. See [the FAQ](docs/faq.md) for the full list of v1 limits.
- It trusts the URLs you give it for Radarr and Sonarr, including addresses on your own LAN (`http://192.168.1.x`, `http://localhost:...`), and does not try to block or warn on them. That's accepted for v1 - Spacearr assumes you're the one configuring your own instances.
- The poster cache under `/config/posters` is never pruned in v1. It only grows as instances and items change; deleting it is safe (posters are refetched on demand) if disk use ever matters to you.

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

Spacearr is written with Claude under human direction; every task ran through an independent AI reviewer pass before being merged, and I review and tag every release myself. Full breakdown in [AI-DISCLOSURE.md](https://github.com/ZOMBiEZ4GIT/Spacearr/blob/main/AI-DISCLOSURE.md).

Report bugs on [GitHub Discussions](https://github.com/ZOMBiEZ4GIT/Spacearr/discussions); I will answer criticism there, not delete it.

---

## r/sonarr

**Title:** Spacearr — the cooler looking WinDirStat for your arr stack (v0.1.0, self-hosted)

Spacearr scans the media library behind Radarr and Sonarr and draws it as a treemap: block size is bytes, colour is bitrate heat, and you can reclaim space through Radarr's and Sonarr's own APIs.

![Library view](https://raw.githubusercontent.com/ZOMBiEZ4GIT/Spacearr/main/docs/screenshots/library-dark.png)

What it does:

- Treemap of your whole library, with a series → season → episode drill-down for Sonarr libraries specifically, posters under the heat colour on large blocks.
- Any number of Radarr and Sonarr instances — the 1080p + 4K two-instance setup is the normal case, and Spacearr shows what it costs in space.
- Duplicates across instances, ranked by wasted bytes, plus two preview-first actions (delete, or replace with a smaller release) — every preview lists exactly what will be asked of Sonarr and how many bytes it frees.

What it does not do:

- It does not transcode. If you want to re-encode files in place, use Tdarr, Unmanic or FileFlows. Spacearr keeps the release and lets the arr app fetch a smaller one.
- It does not delete from disk itself. Deletions go through Radarr's `moviefile` / Sonarr's `episodefile` endpoints, so the arr app's own view of your library stays in sync.
- It does not talk to Plex, Jellyfin or Emby, and it does not know what has been watched. Maintainerr and Janitorr do that.
- It has no rules engine and never acts on its own. Scanning runs on a schedule (default every 6 hours); actions never do. Every action is a person clicking a preview and then a confirm.
- No automatic or scheduled deletions, no bulk replace, no sub-path reverse proxy (root only), no UNC path support on Windows hosts. See [the FAQ](docs/faq.md) for the full list of v1 limits.
- It trusts the URLs you give it for Radarr and Sonarr, including addresses on your own LAN (`http://192.168.1.x`, `http://localhost:...`), and does not try to block or warn on them. That's accepted for v1 - Spacearr assumes you're the one configuring your own instances.
- The poster cache under `/config/posters` is never pruned in v1. It only grows as instances and items change; deleting it is safe (posters are refetched on demand) if disk use ever matters to you.

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

Spacearr is written with Claude under human direction; every task ran through an independent AI reviewer pass before being merged, and I review and tag every release myself. Full breakdown in [AI-DISCLOSURE.md](https://github.com/ZOMBiEZ4GIT/Spacearr/blob/main/AI-DISCLOSURE.md).

Report bugs on [GitHub Discussions](https://github.com/ZOMBiEZ4GIT/Spacearr/discussions); I will answer criticism there, not delete it.

---

## Lemmy `!selfhosted`

**Title:** [AIP] Spacearr — the cooler looking WinDirStat for your arr stack (v0.1.0)

**Disclosure summary:** `[AIP]` — implementation and tests were generated by Claude from a written, human-approved plan; every task also went through an independent AI reviewer pass before merge, and I review and tag every release myself. Full phase-by-phase breakdown in [AI-DISCLOSURE.md](https://github.com/ZOMBiEZ4GIT/Spacearr/blob/main/AI-DISCLOSURE.md).

Spacearr scans the media library behind Radarr and Sonarr and draws it as a treemap: block size is bytes, colour is bitrate heat, and you can reclaim space through Radarr's and Sonarr's own APIs. Nothing leaves your network.

![Library view](https://raw.githubusercontent.com/ZOMBiEZ4GIT/Spacearr/main/docs/screenshots/library-dark.png)

What it does:

- Treemap of your whole library — movies as blocks, series → season → episode drill-down, posters under the heat colour on large blocks.
- Bitrate heat, normalised per codec, with relative (rank within your library) and absolute (fixed thresholds) modes.
- Duplicates across instances, ranked by wasted bytes, plus two preview-first actions (delete, or replace with a smaller release) through the arr apps' own APIs, gated by a confirm step.

What it does not do:

- It does not transcode. If you want to re-encode files in place, use Tdarr, Unmanic or FileFlows. Spacearr keeps the release and lets the arr app fetch a smaller one.
- It does not delete from disk itself. Deletions go through Radarr's `moviefile` / Sonarr's `episodefile` endpoints, so the arr app's own view of your library stays in sync.
- It does not talk to Plex, Jellyfin or Emby, and it does not know what has been watched. Maintainerr and Janitorr do that.
- It has no rules engine and never acts on its own. Scanning runs on a schedule (default every 6 hours); actions never do. Every action is a person clicking a preview and then a confirm.
- No automatic or scheduled deletions, no bulk replace, no sub-path reverse proxy (root only), no UNC path support on Windows hosts. See [the FAQ](docs/faq.md) for the full list of v1 limits.
- It trusts the URLs you give it for Radarr and Sonarr, including addresses on your own LAN (`http://192.168.1.x`, `http://localhost:...`), and does not try to block or warn on them. That's accepted for v1 - Spacearr assumes you're the one configuring your own instances.
- The poster cache under `/config/posters` is never pruned in v1. It only grows as instances and items change; deleting it is safe (posters are refetched on demand) if disk use ever matters to you.

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

Spacearr is written with Claude under human direction; every task ran through an independent AI reviewer pass before being merged, and I review and tag every release myself. Full breakdown in [AI-DISCLOSURE.md](https://github.com/ZOMBiEZ4GIT/Spacearr/blob/main/AI-DISCLOSURE.md).

Report bugs on [GitHub Discussions](https://github.com/ZOMBiEZ4GIT/Spacearr/discussions); I will answer criticism there, not delete it.

---

## awesome-arr PR ("Complimenting Apps")

**PR title:** Add Spacearr to Complimenting Apps

**List entry (their format):**

```
- [Spacearr](https://github.com/ZOMBiEZ4GIT/Spacearr) - Treemap visualisation of your Radarr/Sonarr library, coloured by bitrate heat, with cross-instance duplicate detection and preview-first cleanup through the arr APIs. Self-hosted, GPL-3.0.
```

**PR description:**

Spacearr scans the media library behind Radarr and Sonarr and draws it as a treemap: block size is bytes, colour is bitrate heat, and you can reclaim space through Radarr's and Sonarr's own APIs.

![Library view](https://raw.githubusercontent.com/ZOMBiEZ4GIT/Spacearr/main/docs/screenshots/library-dark.png)

What it does:

- Treemap of your whole library — movies as blocks, series → season → episode drill-down, posters under the heat colour on large blocks.
- Bitrate heat, normalised per codec, so files of comparable visual quality but different codecs are coloured comparably.
- Duplicates across instances, ranked by wasted bytes, plus two preview-first actions (delete, or replace with a smaller release).

What it does not do:

- It does not transcode. If you want to re-encode files in place, use Tdarr, Unmanic or FileFlows. Spacearr keeps the release and lets the arr app fetch a smaller one.
- It does not delete from disk itself. Deletions go through Radarr's `moviefile` / Sonarr's `episodefile` endpoints, so the arr app's own view of your library stays in sync.
- It does not talk to Plex, Jellyfin or Emby, and it does not know what has been watched. Maintainerr and Janitorr do that.
- It has no rules engine and never acts on its own. Scanning runs on a schedule (default every 6 hours); actions never do. Every action is a person clicking a preview and then a confirm.
- No automatic or scheduled deletions, no bulk replace, no sub-path reverse proxy (root only), no UNC path support on Windows hosts. See [the FAQ](docs/faq.md) for the full list of v1 limits.
- It trusts the URLs you give it for Radarr and Sonarr, including addresses on your own LAN (`http://192.168.1.x`, `http://localhost:...`), and does not try to block or warn on them. That's accepted for v1 - Spacearr assumes you're the one configuring your own instances.
- The poster cache under `/config/posters` is never pruned in v1. It only grows as instances and items change; deleting it is safe (posters are refetched on demand) if disk use ever matters to you.

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

Spacearr is written with Claude under human direction; every task ran through an independent AI reviewer pass before being merged, and I review and tag every release myself. Full breakdown in [AI-DISCLOSURE.md](https://github.com/ZOMBiEZ4GIT/Spacearr/blob/main/AI-DISCLOSURE.md).

Report bugs on [GitHub Discussions](https://github.com/ZOMBiEZ4GIT/Spacearr/discussions); I will answer criticism there, not delete it.

---

## Unraid

### Community Applications submission

Submit the template at its raw URL (Community Applications reads this directly, no forum post needed for the listing itself):

```
https://raw.githubusercontent.com/ZOMBiEZ4GIT/Spacearr/main/docker/unraid/spacearr.xml
```

### Support thread opener (Unraid forums)

**Title:** Spacearr — the cooler looking WinDirStat for your arr stack (v0.1.0) [Support]

Spacearr scans the media library behind Radarr and Sonarr and draws it as a treemap: block size is bytes, colour is bitrate heat, and you can reclaim space through Radarr's and Sonarr's own APIs. This is the support thread for the Community Applications template.

![Library view](https://raw.githubusercontent.com/ZOMBiEZ4GIT/Spacearr/main/docs/screenshots/library-dark.png)

What it does:

- Treemap of your whole library — movies as blocks, series → season → episode drill-down, posters under the heat colour on large blocks.
- Bitrate heat, normalised per codec, with relative (rank within your library) and absolute (fixed thresholds) modes.
- Duplicates across instances, ranked by wasted bytes, plus two preview-first actions (delete, or replace with a smaller release) through the arr apps' own APIs.

What it does not do:

- It does not transcode. If you want to re-encode files in place, use Tdarr, Unmanic or FileFlows. Spacearr keeps the release and lets the arr app fetch a smaller one.
- It does not delete from disk itself. Deletions go through Radarr's `moviefile` / Sonarr's `episodefile` endpoints, so the arr app's own view of your library stays in sync.
- It does not talk to Plex, Jellyfin or Emby, and it does not know what has been watched. Maintainerr and Janitorr do that.
- It has no rules engine and never acts on its own. Scanning runs on a schedule (default every 6 hours); actions never do. Every action is a person clicking a preview and then a confirm.
- No automatic or scheduled deletions, no bulk replace, no sub-path reverse proxy (root only), no UNC path support on Windows hosts. See [the FAQ](docs/faq.md) for the full list of v1 limits.
- It trusts the URLs you give it for Radarr and Sonarr, including addresses on your own LAN (`http://192.168.1.x`, `http://localhost:...`), and does not try to block or warn on them. That's accepted for v1 - Spacearr assumes you're the one configuring your own instances.
- The poster cache under `/config/posters` is never pruned in v1. It only grows as instances and items change; deleting it is safe (posters are refetched on demand) if disk use ever matters to you.

The Docker Compose block below is equivalent to the CA template's default mapping (`PUID=99`/`PGID=100` on the template, `1000`/`1000` here — set them to match your own user):

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

Spacearr is written with Claude under human direction; every task ran through an independent AI reviewer pass before being merged, and I review and tag every release myself. Full breakdown in [AI-DISCLOSURE.md](https://github.com/ZOMBiEZ4GIT/Spacearr/blob/main/AI-DISCLOSURE.md).

Report bugs on [GitHub Discussions](https://github.com/ZOMBiEZ4GIT/Spacearr/discussions); I will answer criticism there, not delete it.

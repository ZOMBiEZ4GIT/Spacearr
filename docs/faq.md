# FAQ

**Why is everything red?**
You're probably in absolute heat mode on a library that's mostly remuxes or otherwise genuinely high-bitrate — absolute mode is calibrated against fixed thresholds, and a remux really does spend a lot of bits per pixel on purpose. Switch to relative mode (the default) to rank your library against itself instead of a fixed scale; see [docs/heat.md](heat.md).

**Why is a file unmatched?**
Spacearr scans the disk and separately asks your arr apps what they know, then matches the two by path. An unmatched block is a file Spacearr found but couldn't line up with anything an arr app reported — almost always a missing or wrong path mapping. See [docs/connections-and-paths.md](connections-and-paths.md).

**Does it delete without asking?**
No. Every Delete and Replace is preview-first: you see exactly what will happen and how many bytes it frees before a signed, 10-minute confirmation token lets you actually confirm it. See [docs/actions.md](actions.md).

**Can it run rules automatically?**
No, not in v1, by design. Scanning runs on a schedule (default every 6 hours) so the library view stays current, but nothing ever deletes or replaces a file without a person clicking preview, then confirm.

**Does it work with Plex/Jellyfin?**
It doesn't integrate with a media server at all, and doesn't need to — Spacearr only cares about what's on disk and what Radarr/Sonarr know about it. If you want cleanup driven by watch history, that's Maintainerr's or Janitorr's job, not Spacearr's.

**Why not transcode?**
Re-encoding is a different problem with mature tools already solving it (Tdarr, Unmanic, FileFlows). Spacearr's job is to show you what's taking the space and let you ask the arr app for a different release — it never rewrites a media file itself.

**Does it phone home?**
No. No telemetry, no analytics, no update checker, no third-party fonts or CDN — fonts ship bundled via `@fontsource`. The only outbound traffic is to the Radarr/Sonarr instances you configure, including fetching their posters. Verify it yourself: watch the container's network connections, or read the code — the only HTTP client that talks outward is `ArrHttp`, and only to your own instances.

**Windows/macOS native builds?**
Docker is the first-class, tested path, and the only one with prebuilt images. `dotnet run --project src/Spacearr` (.NET 8 SDK, Node 20, `ffprobe` on `PATH`) works fine on Windows or macOS — there's just no installer for either in v1. UNC paths (`\\server\share`) aren't supported as scan roots on Windows hosts; map a drive letter instead.

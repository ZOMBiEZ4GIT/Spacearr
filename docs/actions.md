# Actions

Spacearr has exactly two actions: **Delete** and **Replace**. Both are preview-first, both go through the arr app's own API, and both are logged. Spacearr never deletes a file from disk itself.

## Delete

Delete asks the arr app to remove the file, and optionally unmonitor so it isn't re-downloaded:

- **Radarr**: `DELETE /api/v3/moviefile/{id}`, then, if checked, `PUT /api/v3/movie/{id}` with `monitored: false`.
- **Sonarr**: `DELETE /api/v3/episodefile/{id}`, then `PUT /api/v3/episode/{id}` per episode on that file (a season pack can cover more than one; the preview lists each).

## Replace

Replace runs three steps, deliberately in this order: set the quality profile (Radarr: on the movie; Sonarr: on the whole series — see below), delete the current file, then ask the arr app to search for a replacement.

The file is deleted *before* the search, not after a smaller release turns up, because Radarr and Sonarr won't download a release that scores worse than what they already have — searching first and hoping for a downgrade wouldn't work. Deleting first means search runs with nothing to compare against, and finds whatever the new profile allows. **This is why the preview shows an estimate, not a guarantee** — based on similar files already in your library at the target quality, or a rough per-quality table otherwise.

## The Sonarr series-wide warning

Sonarr has no per-episode quality profile — profiles are set on the series. Replacing one episode changes the profile for the whole series, and the preview says so before you can confirm; Sonarr itself doesn't support anything narrower.

## Confirmation

Every preview issues a signed, time-limited token (10 minutes, HMAC-SHA256) describing exactly the action previewed. Execute requires that exact token, and the confirm button stays disabled for a moment after the preview loads, so a confirm can't land before you've read it. An expired token makes execute re-preview rather than act on stale information.

## The action log and partial failure

Every executed action — succeeded or failed — writes a row to the action log (`GET /api/v1/actions/log`, on the Activity page): what, which instance, size and quality before/after, outcome, detail. Replace's three steps run in sequence, not atomically — the arr APIs offer no atomic equivalent. Profile change succeeds but delete fails: logged, file untouched. Delete succeeds but search fails or has nothing to search: logged, file already gone — search manually from the arr app. Spacearr drops its record of the file the moment the arr-side delete succeeds, so it never points at a file the arr app has already deleted.

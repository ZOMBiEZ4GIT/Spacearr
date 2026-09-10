# Connections and paths

Spacearr scans the disk itself, then separately asks Radarr/Sonarr what they know about each file, and lines the two up by path. Getting that match right is the one fiddly part of setup.

## How matching works

Radarr/Sonarr reports a path for every file (e.g. `/data/movies/Alpha (2020)/Alpha.mkv`). Spacearr runs it through the instance's path mappings (longest prefix first) to get the path it expects on its own disk, and looks that up against what scanning found. If Spacearr and your arr containers mount media at the same container path — the normal case if you follow the compose example, mounting everything at `/data` — there's nothing to map: the paths are already identical.

When they aren't (different mount points, a NAS under a different name), add a mapping: the prefix as the arr app sees it, and the prefix as Spacearr sees it. Mappings are per-instance. Once a library folder is added, "Suggest mappings" compares the arr app's roots against yours and offers a match as "likely" (one candidate) or "possible" (more than one) for you to confirm.

## The Docker `localhost` trap

Inside a container, `localhost` means that container, not the host or a sibling one. If Radarr also runs in Docker, use its container name and internal port (`http://radarr:7878`) on the same Docker network, not `http://localhost:7878` — the connection form's hint says so for exactly this reason.

## Reading "Matched N of M"

Each connection's card shows `Matched X of Y files. Z unmatched — usually a path mapping is missing.` X is how many of that instance's titles Spacearr found a file for; Y is the total the arr app reports having a file for. A gap almost always means a mapping is missing or wrong — fix it and scan again.

## Multiple instances

Add as many Radarr and Sonarr instances as you like — the 1080p + 4K two-instance setup is the normal case Spacearr is built around, and it's what makes duplicate detection (see [docs/actions.md](actions.md)) worth having: the same title added twice, once per instance, at two different qualities.

## "Unmatched" blocks

A scanned file that couldn't be matched to anything a connected arr app reported shows as an "Unmatched" block: dotted border, no title, just filename and size, no actions available. It's almost always a path mapping problem; it can also mean a stray file the arr apps don't manage.

# Spacearr

Storage visualization and optimization for the *arr media stack.

Spacearr scans your media library, visualizes file sizes and bitrates with an interactive treemap, and helps you reclaim storage by identifying oversized files, duplicates, and quality swap opportunities -- all integrated with Sonarr and Radarr.

## Features

- **WinDirStat-style treemap** -- Rectangle size = file size, color = bitrate heat (green = efficient, red = bloated)
- **Bitrate heat mapping** -- Instantly spot files with unusually high bitrates relative to their resolution
- **Multiple color modes** -- Switch between bitrate, quality profile, codec, or resolution views
- **Space Saver recommendations** -- Automatically identifies files where a quality downgrade saves the most space
- **Duplicate detection** -- Find and resolve multiple copies of the same media
- **Integrated actions** -- Delete, search for replacements, or swap quality profiles directly through Sonarr/Radarr
- **Bulk operations** -- Mass delete or downgrade by filter criteria
- **Tag-based rules** -- Enforce quality limits automatically (e.g., "kids movies never exceed 1080p")
- **Action history** -- Track all changes with running space-saved totals

## Screenshots

> Screenshots coming soon

## Installation

### Docker (Recommended)

Pull and run with a single command:

```bash
docker run -d \
  --name spacearr \
  -p 8787:8787 \
  -v /path/to/config:/config \
  -v /path/to/media:/media \
  -e TZ=Etc/UTC \
  --restart unless-stopped \
  spacearr/spacearr:latest
```

Or use Docker Compose. Create a `docker-compose.yml`:

```yaml
services:
  spacearr:
    image: spacearr/spacearr:latest
    container_name: spacearr
    ports:
      - "8787:8787"
    volumes:
      - ./config:/config
      - /path/to/media:/media
    environment:
      - TZ=Etc/UTC
    restart: unless-stopped
```

Then start it:

```bash
docker compose up -d
```

To build from source instead of using a pre-built image, replace `image: spacearr/spacearr:latest` with `build: .` and run from the repository root.

### Manual

Prerequisites:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20](https://nodejs.org/) (LTS)

Build the frontend:

```bash
npm install --legacy-peer-deps
npm run build
```

Build and run the backend:

```bash
dotnet build src/Spacearr.sln -c Release
dotnet run --project src/NzbDrone.Console/Spacearr.Console.csproj -- --nobrowser --data=./config
```

Spacearr will start on `http://localhost:8787` by default.

## Configuration

On first launch, open the Spacearr UI at `http://localhost:8787` and navigate to **Settings > General** to configure the basics.

### Connecting Sonarr and Radarr

1. Go to **Settings > ARR Connections**.
2. Click **Add Connection** and select Sonarr or Radarr.
3. Enter the URL of your Sonarr/Radarr instance (e.g., `http://localhost:7878` for Radarr).
4. Provide the API key, which you can find in your Sonarr/Radarr instance under **Settings > General > Security**.
5. Click **Test** to verify the connection, then **Save**.

Once connected, Spacearr will pull your media library data from each ARR instance and begin building the treemap visualization.

### Key Settings

| Setting | Description |
|---|---|
| **Port** | Default `8787`. Override with the `SPACEARR__PORT` environment variable. |
| **Data Directory** | Where Spacearr stores its database and configuration. Default `/config` in Docker. |
| **Media Path** | Mount your media library so Spacearr can scan file sizes and bitrates on disk. |

## Tech Stack

- **Backend:** .NET 8 (C#) -- forked from the Radarr architecture
- **Frontend:** React 18 + TypeScript
- **Database:** SQLite
- **Visualization:** d3-hierarchy treemap
- **Real-time updates:** SignalR

## Contributing

Contributions are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) before submitting a pull request.

1. Fork the repository.
2. Create a feature branch from `develop`.
3. Make your changes and add tests where appropriate.
4. Run the frontend linter: `npm run lint`
5. Submit a pull request targeting the `develop` branch.

## License

[GNU GPL v3](http://www.gnu.org/licenses/gpl.html) -- see [LICENSE](LICENSE) for details.

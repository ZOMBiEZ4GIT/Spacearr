# Spacearr Backend Foundation Implementation Plan (Plan 1 of 4)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Radarr fork with a clean standalone ASP.NET Core 8 project that starts, migrates its SQLite database, enforces authentication on every API route, encrypts secrets, and runs background jobs with live progress.

**Architecture:** One `Spacearr` web project using minimal APIs grouped by feature folder, EF Core 8 + SQLite for persistence, a channel-backed `JobRunner` hosted service for background work, and an in-process progress hub streamed to clients over Server-Sent Events. Tests live in `Spacearr.Tests` (xunit) and use an in-memory SQLite connection and `WebApplicationFactory`.

**Tech Stack:** .NET 8 SDK (8.0.4xx), ASP.NET Core minimal APIs, EF Core 8 (`Microsoft.EntityFrameworkCore.Sqlite`), Serilog, Swashbuckle, xunit + FluentAssertions + `Microsoft.AspNetCore.Mvc.Testing`.

**Spec:** `docs/superpowers/specs/2026-09-10-spacearr-standalone-design.md`

## Global Constraints

- Target framework `net8.0`; `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- All API routes live under `/api/v1/`. JSON is camelCase. Times are UTC `DateTime` with `Kind = Utc`.
- Every `/api` route requires auth except `GET /api/v1/system/status`. A test enumerates routes and enforces this.
- Config dir: `SPACEARR_CONFIG_DIR` env var, else `/config` when it exists, else `%LOCALAPPDATA%/Spacearr` (Windows) or `~/.config/spacearr`.
- Port 8787 by default (`SPACEARR_PORT` overrides).
- Arr API keys never leave the server in a response.
- Commit after every task with the trailer lines: `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>` and `Claude-Session: https://claude.ai/code/session_01KCoYzAX74gBst65RVHz82j`.
- Work happens on branch `rebuild`.

## File structure produced by this plan

```
Spacearr.sln
Directory.Build.props                 shared compiler settings
.gitignore                            new, covers bin/obj/node_modules/config
src/Spacearr/Spacearr.csproj
src/Spacearr/Program.cs               composition root; calls Add*/Map* per feature
src/Spacearr/Infrastructure/ConfigPaths.cs        resolves config dir, db path, log dir
src/Spacearr/Infrastructure/SecretProtector.cs    AES-GCM encrypt/decrypt with key file
src/Spacearr/Infrastructure/Clock.cs              IClock for testable time
src/Spacearr/Data/SpacearrDb.cs                   DbContext + entity configuration
src/Spacearr/Data/Entities/*.cs                   one file per entity
src/Spacearr/Data/Migrations/*                    EF migrations (generated)
src/Spacearr/Auth/AuthEndpoints.cs                setup, login, logout, me
src/Spacearr/Auth/ApiKeyAuthHandler.cs            X-Api-Key scheme
src/Spacearr/Auth/UserService.cs                  hashing, api key generation, lockout
src/Spacearr/System/SystemEndpoints.cs            status
src/Spacearr/Settings/SettingsService.cs          typed get/set over Setting table
src/Spacearr/Settings/SettingsEndpoints.cs
src/Spacearr/Jobs/JobRunner.cs                    hosted service, one job at a time
src/Spacearr/Jobs/IJob.cs, JobRequest.cs, JobQueue.cs
src/Spacearr/Jobs/ProgressHub.cs                  in-process pub/sub of progress events
src/Spacearr/Jobs/JobEndpoints.cs                 list, detail, cancel, events (SSE)
src/Spacearr/Jobs/ScanScheduler.cs                periodic enqueue
src/Spacearr.Tests/Spacearr.Tests.csproj
src/Spacearr.Tests/TestApp.cs                     WebApplicationFactory with temp config dir
src/Spacearr.Tests/**/*Tests.cs
```

---

### Task 1: Reset the repository to a clean standalone layout

**Files:**
- Delete: `src/` (entire Radarr fork), `frontend/`, `node_modules/`, `_output/`, `_temp/`, `_tests/`, `radarr-fork/`, `temp-clone/`, `config/`, `distribution/`, `schemas/`, `Logo/`, `build.sh`, `test.sh`, `docs.sh`, `dotnet-install.ps1`, `install-dotnet.ps1`, `azure-pipelines.yml`, `docker-compose.yml`, `Dockerfile`, `package.json`, `package-lock.json`, `yarn.lock`, `tsconfig.json`, `global.json`, `CLA.md`, `CODE_OF_CONDUCT.md`, `CONTRIBUTING.md`, `SECURITY.md`
- Keep: `LICENSE`, `README.md` (rewritten in Plan 4), `docs/`, `.git/`
- Create: `.gitignore`, `global.json`, `Directory.Build.props`

**Interfaces:**
- Produces: an empty tree where later tasks create `src/Spacearr` and `src/Spacearr.Tests`.

- [ ] **Step 1: Confirm you are on the `rebuild` branch**

Run: `git branch --show-current`
Expected: `rebuild`

- [ ] **Step 2: Remove the fork**

Run from repo root (Git Bash):

```bash
git rm -r -q --cached src frontend config distribution schemas Logo build.sh test.sh docs.sh dotnet-install.ps1 install-dotnet.ps1 azure-pipelines.yml docker-compose.yml Dockerfile package.json package-lock.json yarn.lock tsconfig.json global.json CLA.md CODE_OF_CONDUCT.md CONTRIBUTING.md SECURITY.md 2>/dev/null
rm -rf src frontend node_modules _output _temp _tests radarr-fork temp-clone config distribution schemas Logo build.sh test.sh docs.sh dotnet-install.ps1 install-dotnet.ps1 azure-pipelines.yml docker-compose.yml Dockerfile package.json package-lock.json yarn.lock tsconfig.json global.json CLA.md CODE_OF_CONDUCT.md CONTRIBUTING.md SECURITY.md
ls
```

Expected listing: `LICENSE  README.md  docs`

- [ ] **Step 3: Write `.gitignore`**

```gitignore
# .NET
bin/
obj/
*.user
.vs/
TestResults/

# Node
web/node_modules/
web/dist/

# Spacearr runtime data (never commit a database or logs)
config/
*.db
*.db-shm
*.db-wal
logs/

# OS / editor
.DS_Store
Thumbs.db
.vscode/
.idea/
```

- [ ] **Step 4: Write `global.json`** (pin the SDK major so CI and dev match)

```json
{
  "sdk": {
    "version": "8.0.100",
    "rollForward": "latestFeature"
  }
}
```

- [ ] **Step 5: Write `Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
    <InvariantGlobalization>true</InvariantGlobalization>
    <Authors>Roland Taylor</Authors>
    <Product>Spacearr</Product>
    <Copyright>Copyright (c) 2026 Roland Taylor. GPL-3.0.</Copyright>
    <Version>0.1.0</Version>
  </PropertyGroup>
</Project>
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Reset repository for standalone rebuild

Remove the Radarr fork, tracked runtime data and build junk. Keep
LICENSE, README and docs. Add .gitignore, global.json and shared
build props for the new layout.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01KCoYzAX74gBst65RVHz82j"
```

---

### Task 2: Solution, web project, test project, and a passing smoke test

**Files:**
- Create: `Spacearr.sln`, `src/Spacearr/Spacearr.csproj`, `src/Spacearr/Program.cs`, `src/Spacearr.Tests/Spacearr.Tests.csproj`, `src/Spacearr.Tests/TestApp.cs`, `src/Spacearr.Tests/System/SystemStatusTests.cs`, `src/Spacearr/System/SystemEndpoints.cs`, `src/Spacearr/Infrastructure/ConfigPaths.cs`

**Interfaces:**
- Produces: `ConfigPaths` (`string Root`, `string DatabasePath`, `string LogDirectory`, `string SecretKeyPath`), registered as singleton. `TestApp : WebApplicationFactory<Program>` that points `SPACEARR_CONFIG_DIR` at a fresh temp dir per test class. `GET /api/v1/system/status` returning `{ version, setupComplete, tools: { ffprobe: bool, mediainfo: bool } }` (tools are `false` until Plan 2).

- [ ] **Step 1: Create the projects**

```bash
dotnet new sln -n Spacearr
dotnet new web -n Spacearr -o src/Spacearr --framework net8.0
dotnet new xunit -n Spacearr.Tests -o src/Spacearr.Tests --framework net8.0
dotnet sln add src/Spacearr/Spacearr.csproj src/Spacearr.Tests/Spacearr.Tests.csproj
dotnet add src/Spacearr.Tests reference src/Spacearr
dotnet add src/Spacearr.Tests package FluentAssertions --version 6.12.1
dotnet add src/Spacearr.Tests package Microsoft.AspNetCore.Mvc.Testing --version 8.0.8
dotnet add src/Spacearr package Serilog.AspNetCore --version 8.0.2
dotnet add src/Spacearr package Serilog.Sinks.File --version 6.0.0
dotnet add src/Spacearr package Swashbuckle.AspNetCore --version 6.8.1
```

Then overwrite `src/Spacearr/Spacearr.csproj` so the props file is honoured and the test project can see internals:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <RootNamespace>Spacearr</RootNamespace>
    <AssemblyName>Spacearr</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.2" />
    <PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.8.1" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Spacearr.Tests" />
  </ItemGroup>
</Project>
```

Delete the generated `src/Spacearr.Tests/UnitTest1.cs`.

- [ ] **Step 2: Write the failing status test**

`src/Spacearr.Tests/TestApp.cs`:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Spacearr.Tests;

public sealed class TestApp : WebApplicationFactory<Program>
{
    public string ConfigDir { get; } = Path.Combine(Path.GetTempPath(), "spacearr-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(ConfigDir);
        builder.UseSetting("SPACEARR_CONFIG_DIR", ConfigDir);
        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(ConfigDir, recursive: true); } catch { /* best effort */ }
    }
}
```

`src/Spacearr.Tests/System/SystemStatusTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Spacearr.Tests.System;

public class SystemStatusTests : IClassFixture<TestApp>
{
    private readonly HttpClient _client;
    public SystemStatusTests(TestApp app) => _client = app.CreateClient();

    [Fact]
    public async Task Status_is_public_and_reports_setup_incomplete_on_fresh_install()
    {
        var response = await _client.GetAsync("/api/v1/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<StatusDto>();
        body!.Version.Should().NotBeNullOrWhiteSpace();
        body.SetupComplete.Should().BeFalse();
    }

    private sealed record StatusDto(string Version, bool SetupComplete);
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test src/Spacearr.Tests --nologo -v q`
Expected: build error (no `Program` partial class / no route) or 404.

- [ ] **Step 4: Implement ConfigPaths, SystemEndpoints, Program**

`src/Spacearr/Infrastructure/ConfigPaths.cs`:

```csharp
namespace Spacearr.Infrastructure;

public sealed class ConfigPaths
{
    public string Root { get; }
    public string DatabasePath => Path.Combine(Root, "spacearr.db");
    public string LogDirectory => Path.Combine(Root, "logs");
    public string SecretKeyPath => Path.Combine(Root, "secret.key");

    public ConfigPaths(string root)
    {
        Root = Path.GetFullPath(root);
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogDirectory);
    }

    public static string Resolve(IConfiguration configuration)
    {
        var fromEnv = configuration["SPACEARR_CONFIG_DIR"];
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;
        if (Directory.Exists("/config")) return "/config";
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spacearr");
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "spacearr");
    }
}
```

`src/Spacearr/System/SystemEndpoints.cs`:

```csharp
using System.Reflection;

namespace Spacearr.System;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/system/status", () =>
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
            return Results.Ok(new StatusResponse(version, SetupComplete: false, new ToolsStatus(false, false)));
        }).AllowAnonymous().WithName("SystemStatus");
        return app;
    }
}

public sealed record StatusResponse(string Version, bool SetupComplete, ToolsStatus Tools);
public sealed record ToolsStatus(bool Ffprobe, bool Mediainfo);
```

`src/Spacearr/Program.cs`:

```csharp
using Serilog;
using Spacearr.Infrastructure;
using Spacearr.System;

var builder = WebApplication.CreateBuilder(args);

var configRoot = ConfigPaths.Resolve(builder.Configuration);
var paths = new ConfigPaths(configRoot);
builder.Services.AddSingleton(paths);

builder.Host.UseSerilog((ctx, lc) => lc
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(paths.LogDirectory, "spacearr-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7));

var port = builder.Configuration["SPACEARR_PORT"] ?? "8787";
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseSwagger(o => o.RouteTemplate = "api/docs/{documentName}/openapi.json");
app.MapSystemEndpoints();

app.Run();

public partial class Program { }
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test src/Spacearr.Tests --nologo -v q`
Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Scaffold Spacearr web project, tests and public status endpoint

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01KCoYzAX74gBst65RVHz82j"
```

---

### Task 3: Data model, DbContext, migrations, migrate on startup

**Files:**
- Create: `src/Spacearr/Data/SpacearrDb.cs`, `src/Spacearr/Data/Entities/Enums.cs`, `ArrInstance.cs`, `PathMapping.cs`, `RootFolder.cs`, `MediaFile.cs`, `MediaItem.cs`, `Job.cs`, `ActionLog.cs`, `Setting.cs`, `User.cs`, `src/Spacearr/Data/Migrations/*` (generated), `src/Spacearr.Tests/Data/MigrationTests.cs`
- Modify: `src/Spacearr/Program.cs`

**Interfaces:**
- Produces: `SpacearrDb : DbContext` with a `DbSet<T>` per entity; enums `ArrType { Radarr, Sonarr }`, `MediaKind { Movie, Episode }`, `JobType { Scan, Enrich, Action }`, `JobStatus { Queued, Running, Succeeded, Failed, Cancelled }`, `JobTrigger { Manual, Scheduled }`, `ActionType { Delete, Replace }`, `ActionOutcome { Succeeded, Failed }`. Startup applies migrations and refuses to start if a `Setting` with key `schema.appVersion` is newer than the running build.

- [ ] **Step 1: Add packages**

```bash
dotnet add src/Spacearr package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.8
dotnet add src/Spacearr package Microsoft.EntityFrameworkCore.Design --version 8.0.8
dotnet tool install --global dotnet-ef --version 8.0.8
```

- [ ] **Step 2: Write the failing migration test**

`src/Spacearr.Tests/Data/MigrationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Spacearr.Data;

namespace Spacearr.Tests.Data;

public class MigrationTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public MigrationTests(TestApp app) => _app = app;

    [Fact]
    public async Task Fresh_database_has_all_tables_after_startup()
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
        var pending = await db.Database.GetPendingMigrationsAsync();
        pending.Should().BeEmpty();

        var tables = await db.Database
            .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'")
            .ToListAsync();
        tables.Should().Contain(new[] { "ArrInstances", "PathMappings", "RootFolders", "MediaFiles", "MediaItems", "Jobs", "ActionLogs", "Settings", "Users" });
    }

    [Fact]
    public async Task Database_uses_wal_journal_mode()
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
        var mode = await db.Database
            .SqlQueryRaw<string>("SELECT journal_mode AS Value FROM pragma_journal_mode")
            .ToListAsync();
        mode.Single().Should().Be("wal");
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test src/Spacearr.Tests --nologo -v q --filter MigrationTests`
Expected: compile error, `SpacearrDb` missing.

- [ ] **Step 4: Write the entities**

`src/Spacearr/Data/Entities/Enums.cs`:

```csharp
namespace Spacearr.Data.Entities;

public enum ArrType { Radarr, Sonarr }
public enum MediaKind { Movie, Episode }
public enum JobType { Scan, Enrich, Action }
public enum JobStatus { Queued, Running, Succeeded, Failed, Cancelled }
public enum JobTrigger { Manual, Scheduled }
public enum ActionType { Delete, Replace }
public enum ActionOutcome { Succeeded, Failed }
```

`src/Spacearr/Data/Entities/ArrInstance.cs`:

```csharp
namespace Spacearr.Data.Entities;

public sealed class ArrInstance
{
    public int Id { get; set; }
    public ArrType Type { get; set; }
    public string Name { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string ApiKeyEncrypted { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public DateTime? LastSyncAt { get; set; }
    public string? LastSyncError { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PathMapping> PathMappings { get; set; } = new();
}
```

`src/Spacearr/Data/Entities/PathMapping.cs`:

```csharp
namespace Spacearr.Data.Entities;

public sealed class PathMapping
{
    public int Id { get; set; }
    public int ArrInstanceId { get; set; }
    public ArrInstance? ArrInstance { get; set; }
    public string RemotePrefix { get; set; } = "";
    public string LocalPrefix { get; set; } = "";
}
```

`src/Spacearr/Data/Entities/RootFolder.cs`:

```csharp
namespace Spacearr.Data.Entities;

public sealed class RootFolder
{
    public int Id { get; set; }
    public string Path { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public DateTime? LastScanAt { get; set; }
}
```

`src/Spacearr/Data/Entities/MediaFile.cs`:

```csharp
namespace Spacearr.Data.Entities;

public sealed class MediaFile
{
    public int Id { get; set; }
    public string Path { get; set; } = "";
    public int RootFolderId { get; set; }
    public long SizeBytes { get; set; }
    public DateTime ModifiedAt { get; set; }
    public DateTime ScannedAt { get; set; }
    public double? DurationSeconds { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public double? FrameRate { get; set; }
    public string? VideoCodec { get; set; }
    public string? VideoProfile { get; set; }
    public int? BitDepth { get; set; }
    public long? OverallBitrateBps { get; set; }
    public long? VideoBitrateBps { get; set; }
    public string? Container { get; set; }
    public string? AudioSummary { get; set; }
    public long? AudioBitrateBps { get; set; }
    public string? HdrFormat { get; set; }
    public string? ProbeError { get; set; }
}
```

`src/Spacearr/Data/Entities/MediaItem.cs`:

```csharp
namespace Spacearr.Data.Entities;

public sealed class MediaItem
{
    public int Id { get; set; }
    public int ArrInstanceId { get; set; }
    public ArrInstance? ArrInstance { get; set; }
    public int ExternalId { get; set; }
    public MediaKind Kind { get; set; }
    public string Title { get; set; } = "";
    public int? Year { get; set; }
    public int? SeriesId { get; set; }
    public string? SeriesTitle { get; set; }
    public int? SeasonNumber { get; set; }
    public string? EpisodeNumbers { get; set; }
    public int? QualityProfileId { get; set; }
    public string? QualityProfileName { get; set; }
    public string? QualityName { get; set; }
    public bool Monitored { get; set; }
    public string? Tags { get; set; }
    public string? PosterUrl { get; set; }
    public int? TmdbId { get; set; }
    public int? TvdbId { get; set; }
    public string? ImdbId { get; set; }
    public int? MediaFileId { get; set; }
    public MediaFile? MediaFile { get; set; }
    public int? ArrFileId { get; set; }
    public string? ArrPath { get; set; }
    public DateTime SyncedAt { get; set; }
}
```

`src/Spacearr/Data/Entities/Job.cs`:

```csharp
namespace Spacearr.Data.Entities;

public sealed class Job
{
    public int Id { get; set; }
    public JobType Type { get; set; }
    public JobStatus Status { get; set; }
    public JobTrigger Trigger { get; set; }
    public DateTime QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? Summary { get; set; }
    public string? Error { get; set; }
}
```

`src/Spacearr/Data/Entities/ActionLog.cs`:

```csharp
namespace Spacearr.Data.Entities;

public sealed class ActionLog
{
    public int Id { get; set; }
    public DateTime At { get; set; }
    public ActionType Type { get; set; }
    public int? MediaItemId { get; set; }
    public int ArrInstanceId { get; set; }
    public string Title { get; set; } = "";
    public string? Path { get; set; }
    public long SizeBytesBefore { get; set; }
    public string? QualityBefore { get; set; }
    public string? QualityAfter { get; set; }
    public ActionOutcome Outcome { get; set; }
    public string? Detail { get; set; }
}
```

`src/Spacearr/Data/Entities/Setting.cs`:

```csharp
namespace Spacearr.Data.Entities;

public sealed class Setting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
```

`src/Spacearr/Data/Entities/User.cs`:

```csharp
namespace Spacearr.Data.Entities;

public sealed class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
```

- [ ] **Step 5: Write the DbContext**

`src/Spacearr/Data/SpacearrDb.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Spacearr.Data.Entities;

namespace Spacearr.Data;

public sealed class SpacearrDb : DbContext
{
    public SpacearrDb(DbContextOptions<SpacearrDb> options) : base(options) { }

    public DbSet<ArrInstance> ArrInstances => Set<ArrInstance>();
    public DbSet<PathMapping> PathMappings => Set<PathMapping>();
    public DbSet<RootFolder> RootFolders => Set<RootFolder>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<ActionLog> ActionLogs => Set<ActionLog>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Setting>().HasKey(s => s.Key);
        b.Entity<User>().HasIndex(u => u.Username).IsUnique();
        b.Entity<User>().HasIndex(u => u.ApiKey).IsUnique();
        b.Entity<MediaFile>().HasIndex(f => f.Path).IsUnique();
        b.Entity<MediaFile>().HasIndex(f => f.RootFolderId);
        b.Entity<MediaItem>().HasIndex(i => new { i.ArrInstanceId, i.Kind, i.ExternalId }).IsUnique();
        b.Entity<MediaItem>().HasIndex(i => i.MediaFileId);
        b.Entity<MediaItem>().HasOne(i => i.MediaFile).WithMany().HasForeignKey(i => i.MediaFileId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<MediaItem>().HasOne(i => i.ArrInstance).WithMany().HasForeignKey(i => i.ArrInstanceId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PathMapping>().HasOne(m => m.ArrInstance).WithMany(i => i.PathMappings).HasForeignKey(m => m.ArrInstanceId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Job>().HasIndex(j => j.QueuedAt);
        b.Entity<ActionLog>().HasIndex(a => a.At);

        var utc = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var utcNullable = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
        foreach (var entity in b.Model.GetEntityTypes())
        {
            foreach (var prop in entity.GetProperties())
            {
                if (prop.ClrType == typeof(DateTime)) prop.SetValueConverter(utc);
                else if (prop.ClrType == typeof(DateTime?)) prop.SetValueConverter(utcNullable);
            }
        }
    }
}
```

- [ ] **Step 6: Register and migrate in Program.cs**

Add `using Microsoft.EntityFrameworkCore;` and `using Spacearr.Data;` at the top. After `builder.Services.AddSingleton(paths);` add:

```csharp
builder.Services.AddDbContext<SpacearrDb>(o =>
    o.UseSqlite($"Data Source={paths.DatabasePath};Cache=Shared"));
```

After `var app = builder.Build();` add:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    await db.Database.MigrateAsync();
    var appVersion = typeof(Program).Assembly.GetName().Version ?? new Version(0, 0, 0);
    var stored = await db.Settings.FindAsync("schema.appVersion");
    if (stored is not null && Version.TryParse(stored.Value, out var storedVersion) && storedVersion > appVersion)
    {
        throw new InvalidOperationException(
            $"Database was created by Spacearr {storedVersion}, newer than this build {appVersion}. Refusing to start.");
    }
    if (stored is null) db.Settings.Add(new Spacearr.Data.Entities.Setting { Key = "schema.appVersion", Value = appVersion.ToString(3) });
    else stored.Value = appVersion.ToString(3);
    await db.SaveChangesAsync();
}
```

- [ ] **Step 7: Generate the initial migration**

Run: `dotnet ef migrations add Initial --project src/Spacearr --output-dir Data/Migrations`
Expected: `src/Spacearr/Data/Migrations/<timestamp>_Initial.cs` and `SpacearrDbModelSnapshot.cs`. Open the migration and confirm all nine `CreateTable` calls are present.

- [ ] **Step 8: Run tests**

Run: `dotnet test src/Spacearr.Tests --nologo -v q`
Expected: 3 passed.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "Add data model, EF Core SQLite context and initial migration

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01KCoYzAX74gBst65RVHz82j"
```

---

### Task 4: Secret protection (AES-256-GCM with a key file)

**Files:**
- Create: `src/Spacearr/Infrastructure/SecretProtector.cs`, `src/Spacearr.Tests/Infrastructure/SecretProtectorTests.cs`
- Modify: `src/Spacearr/Program.cs`

**Interfaces:**
- Produces: `ISecretProtector { string Protect(string plaintext); string Unprotect(string ciphertext); }` singleton. Ciphertext is base64 of `nonce(12) || tag(16) || cipher`. Key file at `ConfigPaths.SecretKeyPath`: 32 random bytes, mode 0600 on Unix.

- [ ] **Step 1: Write the failing tests**

`src/Spacearr.Tests/Infrastructure/SecretProtectorTests.cs`:

```csharp
using FluentAssertions;
using Spacearr.Infrastructure;

namespace Spacearr.Tests.Infrastructure;

public class SecretProtectorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "spacearr-secret-" + Guid.NewGuid().ToString("N"));
    public SecretProtectorTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void Round_trips_and_is_not_plaintext()
    {
        var p = new SecretProtector(new ConfigPaths(_dir));
        var cipher = p.Protect("abc123apikey");
        cipher.Should().NotContain("abc123apikey");
        p.Unprotect(cipher).Should().Be("abc123apikey");
    }

    [Fact]
    public void Key_file_is_created_once_and_reused()
    {
        var paths = new ConfigPaths(_dir);
        var cipher = new SecretProtector(paths).Protect("x");
        File.Exists(paths.SecretKeyPath).Should().BeTrue();
        new SecretProtector(paths).Unprotect(cipher).Should().Be("x");
    }

    [Fact]
    public void Same_plaintext_gives_different_ciphertext()
    {
        var p = new SecretProtector(new ConfigPaths(_dir));
        p.Protect("same").Should().NotBe(p.Protect("same"));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test src/Spacearr.Tests --nologo -v q --filter SecretProtectorTests`
Expected: compile error, `SecretProtector` missing.

- [ ] **Step 3: Implement**

`src/Spacearr/Infrastructure/SecretProtector.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace Spacearr.Infrastructure;

public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}

public sealed class SecretProtector : ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private readonly byte[] _key;

    public SecretProtector(ConfigPaths paths)
    {
        _key = LoadOrCreateKey(paths.SecretKeyPath);
    }

    private static byte[] LoadOrCreateKey(string path)
    {
        if (File.Exists(path))
        {
            var existing = File.ReadAllBytes(path);
            if (existing.Length == KeySize) return existing;
            throw new InvalidOperationException($"Secret key file {path} is corrupt (expected {KeySize} bytes).");
        }
        var key = RandomNumberGenerator.GetBytes(KeySize);
        File.WriteAllBytes(path, key);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        return key;
    }

    public string Protect(string plaintext)
    {
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);
        var output = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(output, 0);
        tag.CopyTo(output, NonceSize);
        cipher.CopyTo(output, NonceSize + TagSize);
        return Convert.ToBase64String(output);
    }

    public string Unprotect(string ciphertext)
    {
        var input = Convert.FromBase64String(ciphertext);
        var nonce = input.AsSpan(0, NonceSize);
        var tag = input.AsSpan(NonceSize, TagSize);
        var cipher = input.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
```

In `Program.cs`, after the `ConfigPaths` singleton:

```csharp
builder.Services.AddSingleton<Spacearr.Infrastructure.ISecretProtector, Spacearr.Infrastructure.SecretProtector>();
```

- [ ] **Step 4: Run tests**

Run: `dotnet test src/Spacearr.Tests --nologo -v q`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add AES-GCM secret protector backed by a config-dir key file

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01KCoYzAX74gBst65RVHz82j"
```

---

### Task 5: Authentication: setup, login, API key, and the route-wide auth gate

**Files:**
- Create: `src/Spacearr/Auth/UserService.cs`, `src/Spacearr/Auth/ApiKeyAuthHandler.cs`, `src/Spacearr/Auth/AuthEndpoints.cs`, `src/Spacearr/Auth/LoginThrottle.cs`, `src/Spacearr.Tests/Auth/AuthTests.cs`, `src/Spacearr.Tests/Auth/AuthGateTests.cs`, `src/Spacearr.Tests/AuthedClient.cs`
- Modify: `src/Spacearr/Program.cs`, `src/Spacearr/System/SystemEndpoints.cs`

**Interfaces:**
- Produces: `IUserService { Task<bool> IsSetupCompleteAsync(); Task<User> CreateAdminAsync(string username, string password); Task<User?> ValidateAsync(string username, string password); Task<User?> FindByApiKeyAsync(string apiKey); Task<string> RegenerateApiKeyAsync(int userId); Task ChangePasswordAsync(int userId, string newPassword); }`. Endpoints: `POST /api/v1/setup {username,password}` (201, or 409 once complete), `POST /api/v1/auth/login {username,password}` (204 + cookie, 401, 429 after 5 failures/min/IP), `POST /api/v1/auth/logout`, `GET /api/v1/auth/me` → `{ username, apiKey }`, `POST /api/v1/auth/apikey/regenerate` → `{ apiKey }`, `POST /api/v1/auth/password {newPassword}`. Auth schemes: cookie `Spacearr.Auth` and header `X-Api-Key` (also `?apikey=` query for SSE). Default authorization policy = authenticated user; applied to every endpoint via `RequireAuthorization()` on the `/api/v1` group. `AuthedClient.CreateAsync(TestApp)` returns an `HttpClient` that has completed setup and login (cookie) for tests.

- [ ] **Step 1: Write the failing tests**

`src/Spacearr.Tests/AuthedClient.cs`:

```csharp
using System.Net.Http.Json;

namespace Spacearr.Tests;

public static class AuthedClient
{
    public const string Username = "admin";
    public const string Password = "correct horse battery staple";

    public static async Task<HttpClient> CreateAsync(TestApp app)
    {
        var client = app.CreateClient(new() { HandleCookies = true });
        var setup = await client.PostAsJsonAsync("/api/v1/setup", new { username = Username, password = Password });
        if (setup.StatusCode != System.Net.HttpStatusCode.Created && setup.StatusCode != System.Net.HttpStatusCode.Conflict)
            throw new InvalidOperationException($"setup failed: {setup.StatusCode}");
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = Username, password = Password });
        login.EnsureSuccessStatusCode();
        return client;
    }
}
```

`src/Spacearr.Tests/Auth/AuthTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Spacearr.Tests.Auth;

public class AuthTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public AuthTests(TestApp app) => _app = app;

    [Fact]
    public async Task Setup_then_login_then_me_and_setup_is_one_shot()
    {
        var client = await AuthedClient.CreateAsync(_app);
        var me = await client.GetFromJsonAsync<MeDto>("/api/v1/auth/me");
        me!.Username.Should().Be(AuthedClient.Username);
        me.ApiKey.Should().HaveLength(32);

        var again = await _app.CreateClient().PostAsJsonAsync("/api/v1/setup", new { username = "x", password = "yyyyyyyyyyyy" });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var status = await _app.CreateClient().GetFromJsonAsync<StatusDto>("/api/v1/system/status");
        status!.SetupComplete.Should().BeTrue();
    }

    [Fact]
    public async Task Api_key_header_authenticates()
    {
        var cookieClient = await AuthedClient.CreateAsync(_app);
        var me = await cookieClient.GetFromJsonAsync<MeDto>("/api/v1/auth/me");
        var keyClient = _app.CreateClient();
        keyClient.DefaultRequestHeaders.Add("X-Api-Key", me!.ApiKey);
        var response = await keyClient.GetAsync("/api/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Wrong_password_is_401_and_sixth_attempt_is_429()
    {
        await AuthedClient.CreateAsync(_app);
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.9");
        for (var i = 0; i < 5; i++)
        {
            var r = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = AuthedClient.Username, password = "wrong" });
            r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        var sixth = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = AuthedClient.Username, password = "wrong" });
        sixth.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Setup_rejects_short_passwords()
    {
        using var fresh = new TestApp();
        var r = await fresh.CreateClient().PostAsJsonAsync("/api/v1/setup", new { username = "a", password = "short" });
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record MeDto(string Username, string ApiKey);
    private sealed record StatusDto(string Version, bool SetupComplete);
}
```

`src/Spacearr.Tests/Auth/AuthGateTests.cs` (this is the Huntarr test: every route must reject anonymous callers):

```csharp
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Spacearr.Tests.Auth;

public class AuthGateTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public AuthGateTests(TestApp app) => _app = app;

    [Fact]
    public async Task Every_api_route_except_status_rejects_anonymous_requests()
    {
        await AuthedClient.CreateAsync(_app);
        var anon = _app.CreateClient();
        var sources = _app.Services.GetRequiredService<IEnumerable<EndpointDataSource>>();
        var routes = sources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith("/api/"))
            .Select(e => (Pattern: e.RoutePattern.RawText!, Methods: e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? new[] { "GET" }))
            .ToList();
        routes.Should().NotBeEmpty();

        var failures = new List<string>();
        foreach (var (pattern, methods) in routes)
        {
            if (pattern == "/api/v1/system/status" || pattern == "/api/v1/setup" || pattern == "/api/v1/auth/login") continue;
            var path = pattern.Replace("{id}", "1").Replace("{itemId}", "1").Replace("{jobId}", "1").Replace("{instanceId}", "1");
            foreach (var method in methods)
            {
                var response = await anon.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
                if (response.StatusCode != HttpStatusCode.Unauthorized)
                    failures.Add($"{method} {path} -> {(int)response.StatusCode}");
            }
        }
        failures.Should().BeEmpty("every API route must require authentication");
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test src/Spacearr.Tests --nologo -v q --filter "AuthTests|AuthGateTests"`
Expected: failures (404 on setup, status still reports false).

- [ ] **Step 3: Implement UserService and LoginThrottle**

`src/Spacearr/Auth/UserService.cs`:

```csharp
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Spacearr.Data;
using Spacearr.Data.Entities;

namespace Spacearr.Auth;

public interface IUserService
{
    Task<bool> IsSetupCompleteAsync();
    Task<User> CreateAdminAsync(string username, string password);
    Task<User?> ValidateAsync(string username, string password);
    Task<User?> FindByApiKeyAsync(string apiKey);
    Task<User?> FindByIdAsync(int id);
    Task<string> RegenerateApiKeyAsync(int userId);
    Task ChangePasswordAsync(int userId, string newPassword);
}

public sealed class UserService : IUserService
{
    private readonly SpacearrDb _db;
    private readonly PasswordHasher<User> _hasher = new();

    public UserService(SpacearrDb db) => _db = db;

    public Task<bool> IsSetupCompleteAsync() => _db.Users.AnyAsync();

    public async Task<User> CreateAdminAsync(string username, string password)
    {
        var user = new User { Username = username.Trim(), ApiKey = NewApiKey(), CreatedAt = DateTime.UtcNow };
        user.PasswordHash = _hasher.HashPassword(user, password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task<User?> ValidateAsync(string username, string password)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Username == username.Trim());
        if (user is null) return null;
        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Failed ? null : user;
    }

    public Task<User?> FindByApiKeyAsync(string apiKey) =>
        _db.Users.SingleOrDefaultAsync(u => u.ApiKey == apiKey);

    public Task<User?> FindByIdAsync(int id) =>
        _db.Users.SingleOrDefaultAsync(u => u.Id == id);

    public async Task<string> RegenerateApiKeyAsync(int userId)
    {
        var user = await _db.Users.SingleAsync(u => u.Id == userId);
        user.ApiKey = NewApiKey();
        await _db.SaveChangesAsync();
        return user.ApiKey;
    }

    public async Task ChangePasswordAsync(int userId, string newPassword)
    {
        var user = await _db.Users.SingleAsync(u => u.Id == userId);
        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        await _db.SaveChangesAsync();
    }

    private static string NewApiKey() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
```

`src/Spacearr/Auth/LoginThrottle.cs`:

```csharp
using System.Collections.Concurrent;

namespace Spacearr.Auth;

public sealed class LoginThrottle
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _failures = new();

    public bool IsLocked(string key)
    {
        if (!_failures.TryGetValue(key, out var entry)) return false;
        if (DateTime.UtcNow - entry.WindowStart > Window) { _failures.TryRemove(key, out _); return false; }
        return entry.Count >= MaxFailures;
    }

    public void RecordFailure(string key)
    {
        _failures.AddOrUpdate(key, _ => (1, DateTime.UtcNow), (_, e) =>
            DateTime.UtcNow - e.WindowStart > Window ? (1, DateTime.UtcNow) : (e.Count + 1, e.WindowStart));
    }

    public void Reset(string key) => _failures.TryRemove(key, out _);
}
```

- [ ] **Step 4: Implement the API key handler**

`src/Spacearr/Auth/ApiKeyAuthHandler.cs`:

```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Spacearr.Auth;

public sealed class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Scheme = "ApiKey";
    private readonly IUserService _users;

    public ApiKeyAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IUserService users)
        : base(options, logger, encoder) => _users = users;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? key = Request.Headers["X-Api-Key"].FirstOrDefault() ?? Request.Query["apikey"].FirstOrDefault();
        if (string.IsNullOrEmpty(key)) return AuthenticateResult.NoResult();
        var user = await _users.FindByApiKeyAsync(key);
        if (user is null) return AuthenticateResult.Fail("Invalid API key");
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
        }, Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme));
    }
}
```

- [ ] **Step 5: Implement the endpoints**

`src/Spacearr/Auth/AuthEndpoints.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Spacearr.Auth;

public sealed record SetupRequest(string Username, string Password);
public sealed record LoginRequest(string Username, string Password);
public sealed record ChangePasswordRequest(string NewPassword);
public sealed record MeResponse(string Username, string ApiKey);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/setup", async (SetupRequest req, IUserService users) =>
        {
            if (await users.IsSetupCompleteAsync()) return Results.Conflict(new { error = "Setup is already complete." });
            if (string.IsNullOrWhiteSpace(req.Username) || req.Username.Length > 64) return Results.BadRequest(new { error = "Username is required (max 64 characters)." });
            if (req.Password is null || req.Password.Length < 10) return Results.BadRequest(new { error = "Password must be at least 10 characters." });
            var user = await users.CreateAdminAsync(req.Username, req.Password);
            return Results.Created("/api/v1/auth/me", new MeResponse(user.Username, user.ApiKey));
        }).AllowAnonymous();

        app.MapPost("/api/v1/auth/login", async (LoginRequest req, HttpContext http, IUserService users, LoginThrottle throttle) =>
        {
            var key = http.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                      ?? http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (throttle.IsLocked(key)) return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            var user = await users.ValidateAsync(req.Username ?? "", req.Password ?? "");
            if (user is null) { throttle.RecordFailure(key); return Results.Unauthorized(); }
            throttle.Reset(key);
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
            }, CookieAuthenticationDefaults.AuthenticationScheme);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14) });
            return Results.NoContent();
        }).AllowAnonymous();

        var group = app.MapGroup("/api/v1/auth").RequireAuthorization();

        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        group.MapGet("/me", async (ClaimsPrincipal principal, IUserService users) =>
        {
            var user = await users.FindByIdAsync(UserId(principal));
            return user is null ? Results.Unauthorized() : Results.Ok(new MeResponse(user.Username, user.ApiKey));
        });

        group.MapPost("/apikey/regenerate", async (ClaimsPrincipal principal, IUserService users) =>
            Results.Ok(new { apiKey = await users.RegenerateApiKeyAsync(UserId(principal)) }));

        group.MapPost("/password", async (ChangePasswordRequest req, ClaimsPrincipal principal, IUserService users) =>
        {
            if (req.NewPassword is null || req.NewPassword.Length < 10) return Results.BadRequest(new { error = "Password must be at least 10 characters." });
            await users.ChangePasswordAsync(UserId(principal), req.NewPassword);
            return Results.NoContent();
        });

        return app;
    }

    public static int UserId(ClaimsPrincipal principal) =>
        int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
```

- [ ] **Step 6: Wire authentication in Program.cs and make status truthful**

In `Program.cs`, before `var app = builder.Build();`:

```csharp
builder.Services.AddScoped<Spacearr.Auth.IUserService, Spacearr.Auth.UserService>();
builder.Services.AddSingleton<Spacearr.Auth.LoginThrottle>();
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "Spacearr.Auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.SlidingExpiration = true;
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    })
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, Spacearr.Auth.ApiKeyAuthHandler>(Spacearr.Auth.ApiKeyAuthHandler.Scheme, _ => { });
builder.Services.AddAuthorization(o =>
{
    o.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
            Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme,
            Spacearr.Auth.ApiKeyAuthHandler.Scheme)
        .RequireAuthenticatedUser().Build();
    o.FallbackPolicy = o.DefaultPolicy;
});
```

After `app.UseSerilogRequestLogging();`:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

Replace `app.MapSystemEndpoints();` with:

```csharp
app.MapSystemEndpoints();
app.MapAuthEndpoints();
```

The `FallbackPolicy` is what makes the gate test meaningful: any endpoint that forgets `RequireAuthorization()` is still protected, and only explicit `AllowAnonymous()` opts out.

Update `SystemEndpoints.cs` to report real setup state:

```csharp
app.MapGet("/api/v1/system/status", async (Spacearr.Auth.IUserService users) =>
{
    var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
    return Results.Ok(new StatusResponse(version, await users.IsSetupCompleteAsync(), new ToolsStatus(false, false)));
}).AllowAnonymous().WithName("SystemStatus");
```

- [ ] **Step 7: Run tests**

Run: `dotnet test src/Spacearr.Tests --nologo -v q`
Expected: all pass, including the gate test.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "Add mandatory auth: setup, cookie login, API key scheme, lockout, route gate test

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01KCoYzAX74gBst65RVHz82j"
```

---

### Task 6: Typed settings service and endpoints

**Files:**
- Create: `src/Spacearr/Settings/SettingsService.cs`, `src/Spacearr/Settings/SettingsEndpoints.cs`, `src/Spacearr.Tests/Settings/SettingsTests.cs`
- Modify: `src/Spacearr/Program.cs`

**Interfaces:**
- Produces: `ISettingsService { Task<AppSettings> GetAsync(); Task SaveAsync(AppSettings s); }` (scoped). `AppSettings` record: `int ScanIntervalHours = 6`, `string[] Extensions = [".mkv",".mp4",".avi",".m4v",".ts",".mov",".wmv",".webm",".mpg"]`, `string? FfprobePath`, `string? MediainfoPath`, `string HeatMode = "relative"`, `string Theme = "dark"`. Endpoints `GET /api/v1/settings` and `PUT /api/v1/settings` (validates 0 ≤ interval ≤ 168, extensions non-empty and dot-prefixed, heatMode in {relative, absolute}).

- [ ] **Step 1: Write the failing test**

`src/Spacearr.Tests/Settings/SettingsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Spacearr.Tests.Settings;

public class SettingsTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public SettingsTests(TestApp app) => _app = app;

    [Fact]
    public async Task Defaults_then_update_round_trip()
    {
        var client = await AuthedClient.CreateAsync(_app);
        var defaults = await client.GetFromJsonAsync<SettingsDto>("/api/v1/settings");
        defaults!.ScanIntervalHours.Should().Be(6);
        defaults.Extensions.Should().Contain(".mkv");
        defaults.HeatMode.Should().Be("relative");

        var put = await client.PutAsJsonAsync("/api/v1/settings", defaults with { ScanIntervalHours = 12, HeatMode = "absolute" });
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var updated = await client.GetFromJsonAsync<SettingsDto>("/api/v1/settings");
        updated!.ScanIntervalHours.Should().Be(12);
        updated.HeatMode.Should().Be("absolute");
    }

    [Fact]
    public async Task Rejects_invalid_values()
    {
        var client = await AuthedClient.CreateAsync(_app);
        var current = await client.GetFromJsonAsync<SettingsDto>("/api/v1/settings");
        var bad = await client.PutAsJsonAsync("/api/v1/settings", current! with { ScanIntervalHours = 999 });
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var badExt = await client.PutAsJsonAsync("/api/v1/settings", current with { Extensions = new[] { "mkv" } });
        badExt.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record SettingsDto(int ScanIntervalHours, string[] Extensions, string? FfprobePath, string? MediainfoPath, string HeatMode, string Theme);
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test src/Spacearr.Tests --nologo -v q --filter SettingsTests`
Expected: 404 failures.

- [ ] **Step 3: Implement**

`src/Spacearr/Settings/SettingsService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Spacearr.Data;
using Spacearr.Data.Entities;

namespace Spacearr.Settings;

public sealed record AppSettings(
    int ScanIntervalHours,
    string[] Extensions,
    string? FfprobePath,
    string? MediainfoPath,
    string HeatMode,
    string Theme)
{
    public static readonly string[] DefaultExtensions = { ".mkv", ".mp4", ".avi", ".m4v", ".ts", ".mov", ".wmv", ".webm", ".mpg" };
    public static AppSettings Defaults => new(6, DefaultExtensions, null, null, "relative", "dark");

    public string? Validate()
    {
        if (ScanIntervalHours < 0 || ScanIntervalHours > 168) return "Scan interval must be between 0 (disabled) and 168 hours.";
        if (Extensions is null || Extensions.Length == 0) return "At least one file extension is required.";
        if (Extensions.Any(e => string.IsNullOrWhiteSpace(e) || !e.StartsWith('.'))) return "Extensions must start with a dot, e.g. .mkv.";
        if (HeatMode is not ("relative" or "absolute")) return "Heat mode must be 'relative' or 'absolute'.";
        if (Theme is not ("dark" or "light")) return "Theme must be 'dark' or 'light'.";
        return null;
    }
}

public interface ISettingsService
{
    Task<AppSettings> GetAsync();
    Task SaveAsync(AppSettings settings);
}

public sealed class SettingsService : ISettingsService
{
    private readonly SpacearrDb _db;
    public SettingsService(SpacearrDb db) => _db = db;

    public async Task<AppSettings> GetAsync()
    {
        var rows = await _db.Settings.ToDictionaryAsync(s => s.Key, s => s.Value);
        var d = AppSettings.Defaults;
        return new AppSettings(
            rows.TryGetValue("scan.intervalHours", out var i) && int.TryParse(i, out var iv) ? iv : d.ScanIntervalHours,
            rows.TryGetValue("scan.extensions", out var e) ? e.Split(',', StringSplitOptions.RemoveEmptyEntries) : d.Extensions,
            rows.GetValueOrDefault("tools.ffprobePath"),
            rows.GetValueOrDefault("tools.mediainfoPath"),
            rows.GetValueOrDefault("heat.mode") ?? d.HeatMode,
            rows.GetValueOrDefault("ui.theme") ?? d.Theme);
    }

    public async Task SaveAsync(AppSettings s)
    {
        await Upsert("scan.intervalHours", s.ScanIntervalHours.ToString());
        await Upsert("scan.extensions", string.Join(',', s.Extensions.Select(x => x.Trim().ToLowerInvariant())));
        await Upsert("tools.ffprobePath", s.FfprobePath ?? "");
        await Upsert("tools.mediainfoPath", s.MediainfoPath ?? "");
        await Upsert("heat.mode", s.HeatMode);
        await Upsert("ui.theme", s.Theme);
        await _db.SaveChangesAsync();
    }

    private async Task Upsert(string key, string value)
    {
        var row = await _db.Settings.FindAsync(key);
        if (row is null) _db.Settings.Add(new Setting { Key = key, Value = value });
        else row.Value = value;
    }
}
```

`src/Spacearr/Settings/SettingsEndpoints.cs`:

```csharp
namespace Spacearr.Settings;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/settings").RequireAuthorization();
        group.MapGet("/", async (ISettingsService settings) => Results.Ok(await settings.GetAsync()));
        group.MapPut("/", async (AppSettings incoming, ISettingsService settings) =>
        {
            var error = incoming.Validate();
            if (error is not null) return Results.BadRequest(new { error });
            await settings.SaveAsync(incoming);
            return Results.NoContent();
        });
        return app;
    }
}
```

Note: `GetValueOrDefault` on the dictionary returns `""` for tool paths that were saved empty; normalise in `GetAsync` by mapping empty strings to null:

```csharp
static string? NullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;
```

and wrap the two tool path reads with it.

In `Program.cs`: `builder.Services.AddScoped<Spacearr.Settings.ISettingsService, Spacearr.Settings.SettingsService>();` and `app.MapSettingsEndpoints();`.

- [ ] **Step 4: Run tests**

Run: `dotnet test src/Spacearr.Tests --nologo -v q`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add typed settings service and endpoints

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01KCoYzAX74gBst65RVHz82j"
```

---

### Task 7: Job queue, runner, progress hub, SSE stream, job endpoints, scheduler

**Files:**
- Create: `src/Spacearr/Jobs/IJob.cs`, `src/Spacearr/Jobs/JobQueue.cs`, `src/Spacearr/Jobs/ProgressHub.cs`, `src/Spacearr/Jobs/JobRunner.cs`, `src/Spacearr/Jobs/JobEndpoints.cs`, `src/Spacearr/Jobs/ScanScheduler.cs`, `src/Spacearr/Infrastructure/Clock.cs`, `src/Spacearr.Tests/Jobs/JobRunnerTests.cs`, `src/Spacearr.Tests/Jobs/JobEndpointTests.cs`
- Modify: `src/Spacearr/Program.cs`

**Interfaces:**
- Produces:
  - `IClock { DateTime UtcNow { get; } }` with `SystemClock`.
  - `IJob { JobType Type { get; } Task<JobSummary> RunAsync(JobContext ctx, CancellationToken ct); }`.
  - `JobContext(int JobId, IServiceProvider Services, IProgressHub Progress)` with helper `Report(string phase, int done, int total, string? detail = null)`.
  - `JobSummary` record: `int FilesSeen, int FilesProbed, int FilesAdded, int FilesRemoved, int ItemsMatched, int ItemsUnmatched, string[] Errors` (all default 0 / empty); serialised to `Job.Summary` as JSON.
  - `IJobQueue { Task<int> EnqueueAsync(JobType type, JobTrigger trigger, Func<IServiceProvider, IJob> factory); bool TryCancel(int jobId); int? RunningJobId { get; } }`. Enqueue creates the `Job` row (Queued) and returns its id. Enqueueing a Scan or Enrich while one of the same type is Queued/Running returns the existing id instead of a duplicate.
  - `IProgressHub { void Publish(ProgressEvent e); IAsyncEnumerable<ProgressEvent> Subscribe(CancellationToken ct); }`; `ProgressEvent(int JobId, JobType Type, string Kind /* "progress" | "finished" */, string? Phase, int Done, int Total, string? Detail, JobStatus? Status)`.
  - Endpoints: `POST /api/v1/jobs/scan` and `POST /api/v1/jobs/enrich` → `{ jobId }` (202). The job factories are registered by Plan 2; until then they enqueue a `NoOpJob` that reports 1/1 and succeeds, so the pipeline is testable now. `GET /api/v1/jobs?page&pageSize` → newest first; `GET /api/v1/jobs/{id}`; `POST /api/v1/jobs/{id}/cancel` (204, or 409 if not cancellable); `GET /api/v1/events` → SSE, `event: progress` / `event: finished`, JSON data, 15 s keepalive comments.
  - `ScanScheduler` hosted service: every 5 minutes checks `AppSettings.ScanIntervalHours`; if > 0 and the last Scan job finished more than that many hours ago (or none exists), enqueues a Scheduled scan.

- [ ] **Step 1: Write the failing runner test**

`src/Spacearr.Tests/Jobs/JobRunnerTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Jobs;

namespace Spacearr.Tests.Jobs;

public class JobRunnerTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public JobRunnerTests(TestApp app) => _app = app;

    private sealed class CountingJob : IJob
    {
        public JobType Type => JobType.Scan;
        public async Task<JobSummary> RunAsync(JobContext ctx, CancellationToken ct)
        {
            for (var i = 1; i <= 3; i++)
            {
                ct.ThrowIfCancellationRequested();
                ctx.Report("counting", i, 3);
                await Task.Delay(10, ct);
            }
            return new JobSummary(FilesSeen: 3);
        }
    }

    private sealed class FailingJob : IJob
    {
        public JobType Type => JobType.Enrich;
        public Task<JobSummary> RunAsync(JobContext ctx, CancellationToken ct) => throw new InvalidOperationException("boom");
    }

    [Fact]
    public async Task Runs_job_records_status_summary_and_publishes_progress()
    {
        var queue = _app.Services.GetRequiredService<IJobQueue>();
        var hub = _app.Services.GetRequiredService<IProgressHub>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var events = new List<ProgressEvent>();
        var listener = Task.Run(async () =>
        {
            await foreach (var e in hub.Subscribe(cts.Token))
            {
                events.Add(e);
                if (e.Kind == "finished" && e.Type == JobType.Scan) break;
            }
        });

        var id = await queue.EnqueueAsync(JobType.Scan, JobTrigger.Manual, _ => new CountingJob());
        await listener;

        using var scope = _app.Services.CreateScope();
        var job = await scope.ServiceProvider.GetRequiredService<SpacearrDb>().Jobs.SingleAsync(j => j.Id == id);
        job.Status.Should().Be(JobStatus.Succeeded);
        job.StartedAt.Should().NotBeNull();
        job.FinishedAt.Should().NotBeNull();
        job.Summary.Should().Contain("\"filesSeen\":3");
        events.Count(e => e.Kind == "progress" && e.JobId == id).Should().Be(3);
        events.Last().Status.Should().Be(JobStatus.Succeeded);
    }

    [Fact]
    public async Task Failed_job_records_error()
    {
        var queue = _app.Services.GetRequiredService<IJobQueue>();
        var id = await queue.EnqueueAsync(JobType.Enrich, JobTrigger.Manual, _ => new FailingJob());
        await WaitForFinish(id);
        using var scope = _app.Services.CreateScope();
        var job = await scope.ServiceProvider.GetRequiredService<SpacearrDb>().Jobs.SingleAsync(j => j.Id == id);
        job.Status.Should().Be(JobStatus.Failed);
        job.Error.Should().Contain("boom");
    }

    [Fact]
    public async Task Duplicate_scan_enqueue_returns_existing_id()
    {
        var queue = _app.Services.GetRequiredService<IJobQueue>();
        var first = await queue.EnqueueAsync(JobType.Scan, JobTrigger.Manual, _ => new CountingJob());
        var second = await queue.EnqueueAsync(JobType.Scan, JobTrigger.Manual, _ => new CountingJob());
        second.Should().Be(first);
        await WaitForFinish(first);
    }

    private async Task WaitForFinish(int id)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = _app.Services.CreateScope();
            var job = await scope.ServiceProvider.GetRequiredService<SpacearrDb>().Jobs.AsNoTracking().SingleAsync(j => j.Id == id);
            if (job.Status is JobStatus.Succeeded or JobStatus.Failed or JobStatus.Cancelled) return;
            await Task.Delay(25);
        }
        throw new TimeoutException($"job {id} did not finish");
    }
}
```

- [ ] **Step 2: Write the failing endpoint test**

`src/Spacearr.Tests/Jobs/JobEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Spacearr.Tests.Jobs;

public class JobEndpointTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public JobEndpointTests(TestApp app) => _app = app;

    [Fact]
    public async Task Scan_enqueues_and_appears_in_list_and_detail()
    {
        var client = await AuthedClient.CreateAsync(_app);
        var accepted = await client.PostAsync("/api/v1/jobs/scan", null);
        accepted.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await accepted.Content.ReadFromJsonAsync<EnqueueDto>();
        body!.JobId.Should().BeGreaterThan(0);

        var detail = await client.GetFromJsonAsync<JobDto>($"/api/v1/jobs/{body.JobId}");
        detail!.Type.Should().Be("scan");

        var list = await client.GetFromJsonAsync<PageDto<JobDto>>("/api/v1/jobs?page=1&pageSize=10");
        list!.Items.Should().Contain(j => j.Id == body.JobId);
    }

    [Fact]
    public async Task Events_stream_sends_finished_event_for_a_job()
    {
        var client = await AuthedClient.CreateAsync(_app);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/events");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");

        var enqueue = await client.PostAsync("/api/v1/jobs/enrich", null);
        var id = (await enqueue.Content.ReadFromJsonAsync<EnqueueDto>())!.JobId;

        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cts.Token));
        string? line;
        var sawFinished = false;
        while ((line = await reader.ReadLineAsync(cts.Token)) is not null)
        {
            if (line.StartsWith("event: finished")) sawFinished = true;
            if (sawFinished && line.StartsWith("data:") && line.Contains($"\"jobId\":{id}")) break;
        }
        sawFinished.Should().BeTrue();
    }

    private sealed record EnqueueDto(int JobId);
    private sealed record JobDto(int Id, string Type, string Status, string Trigger);
    private sealed record PageDto<T>(T[] Items, int Total, int Page, int PageSize);
}
```

- [ ] **Step 3: Run to verify they fail**

Run: `dotnet test src/Spacearr.Tests --nologo -v q --filter "JobRunnerTests|JobEndpointTests"`
Expected: compile errors for the missing `Spacearr.Jobs` types.

- [ ] **Step 4: Implement the clock and job contracts**

`src/Spacearr/Infrastructure/Clock.cs`:

```csharp
namespace Spacearr.Infrastructure;

public interface IClock { DateTime UtcNow { get; } }
public sealed class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
```

`src/Spacearr/Jobs/IJob.cs`:

```csharp
using Spacearr.Data.Entities;

namespace Spacearr.Jobs;

public interface IJob
{
    JobType Type { get; }
    Task<JobSummary> RunAsync(JobContext ctx, CancellationToken ct);
}

public sealed record JobSummary(
    int FilesSeen = 0,
    int FilesProbed = 0,
    int FilesAdded = 0,
    int FilesRemoved = 0,
    int ItemsMatched = 0,
    int ItemsUnmatched = 0,
    string[]? Errors = null)
{
    public string[] Errors { get; init; } = Errors ?? Array.Empty<string>();
}

public sealed record JobContext(int JobId, JobType Type, IServiceProvider Services, IProgressHub Progress)
{
    public void Report(string phase, int done, int total, string? detail = null) =>
        Progress.Publish(new ProgressEvent(JobId, Type, "progress", phase, done, total, detail, null));
}

public sealed record ProgressEvent(int JobId, JobType Type, string Kind, string? Phase, int Done, int Total, string? Detail, JobStatus? Status);

public sealed class NoOpJob : IJob
{
    public NoOpJob(JobType type) => Type = type;
    public JobType Type { get; }
    public Task<JobSummary> RunAsync(JobContext ctx, CancellationToken ct)
    {
        ctx.Report("noop", 1, 1);
        return Task.FromResult(new JobSummary());
    }
}
```

- [ ] **Step 5: Implement the progress hub**

`src/Spacearr/Jobs/ProgressHub.cs`:

```csharp
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Spacearr.Jobs;

public interface IProgressHub
{
    void Publish(ProgressEvent e);
    IAsyncEnumerable<ProgressEvent> Subscribe(CancellationToken ct);
    ProgressEvent? LastFor(int jobId);
}

public sealed class ProgressHub : IProgressHub
{
    private readonly ConcurrentDictionary<Guid, Channel<ProgressEvent>> _subscribers = new();
    private readonly ConcurrentDictionary<int, ProgressEvent> _last = new();

    public void Publish(ProgressEvent e)
    {
        _last[e.JobId] = e;
        foreach (var channel in _subscribers.Values)
            channel.Writer.TryWrite(e);
    }

    public ProgressEvent? LastFor(int jobId) => _last.TryGetValue(jobId, out var e) ? e : null;

    public async IAsyncEnumerable<ProgressEvent> Subscribe([EnumeratorCancellation] CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<ProgressEvent>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.DropOldest });
        _subscribers[id] = channel;
        try
        {
            await foreach (var e in channel.Reader.ReadAllAsync(ct))
                yield return e;
        }
        finally
        {
            _subscribers.TryRemove(id, out _);
        }
    }
}
```

- [ ] **Step 6: Implement the queue and runner**

`src/Spacearr/Jobs/JobQueue.cs`:

```csharp
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Infrastructure;

namespace Spacearr.Jobs;

public sealed record JobRequest(int JobId, JobType Type, Func<IServiceProvider, IJob> Factory);

public interface IJobQueue
{
    Task<int> EnqueueAsync(JobType type, JobTrigger trigger, Func<IServiceProvider, IJob> factory);
    bool TryCancel(int jobId);
    int? RunningJobId { get; }
    ChannelReader<JobRequest> Reader { get; }
    void MarkRunning(int jobId, CancellationTokenSource cts);
    void MarkFinished(int jobId);
}

public sealed class JobQueue : IJobQueue
{
    private readonly Channel<JobRequest> _channel = Channel.CreateUnbounded<JobRequest>();
    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly object _gate = new();
    private int? _running;
    private CancellationTokenSource? _runningCts;

    public JobQueue(IServiceScopeFactory scopes, IClock clock) { _scopes = scopes; _clock = clock; }

    public ChannelReader<JobRequest> Reader => _channel.Reader;
    public int? RunningJobId { get { lock (_gate) return _running; } }

    public async Task<int> EnqueueAsync(JobType type, JobTrigger trigger, Func<IServiceProvider, IJob> factory)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
        if (type is JobType.Scan or JobType.Enrich)
        {
            var existing = await db.Jobs.Where(j => j.Type == type && (j.Status == JobStatus.Queued || j.Status == JobStatus.Running))
                .OrderBy(j => j.Id).Select(j => (int?)j.Id).FirstOrDefaultAsync();
            if (existing is not null) return existing.Value;
        }
        var job = new Job { Type = type, Status = JobStatus.Queued, Trigger = trigger, QueuedAt = _clock.UtcNow };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        await _channel.Writer.WriteAsync(new JobRequest(job.Id, type, factory));
        return job.Id;
    }

    public bool TryCancel(int jobId)
    {
        lock (_gate)
        {
            if (_running == jobId && _runningCts is not null) { _runningCts.Cancel(); return true; }
        }
        return false;
    }

    public void MarkRunning(int jobId, CancellationTokenSource cts) { lock (_gate) { _running = jobId; _runningCts = cts; } }
    public void MarkFinished(int jobId) { lock (_gate) { if (_running == jobId) { _running = null; _runningCts = null; } } }
}
```

`src/Spacearr/Jobs/JobRunner.cs`:

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Infrastructure;

namespace Spacearr.Jobs;

public sealed class JobRunner : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IJobQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly IProgressHub _hub;
    private readonly IClock _clock;
    private readonly ILogger<JobRunner> _log;

    public JobRunner(IJobQueue queue, IServiceScopeFactory scopes, IProgressHub hub, IClock clock, ILogger<JobRunner> log)
    { _queue = queue; _scopes = scopes; _hub = hub; _clock = clock; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _queue.MarkRunning(request.JobId, jobCts);
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
            var job = await db.Jobs.SingleAsync(j => j.Id == request.JobId, stoppingToken);
            job.Status = JobStatus.Running;
            job.StartedAt = _clock.UtcNow;
            await db.SaveChangesAsync(stoppingToken);

            JobStatus final;
            try
            {
                var instance = request.Factory(scope.ServiceProvider);
                var summary = await instance.RunAsync(new JobContext(job.Id, job.Type, scope.ServiceProvider, _hub), jobCts.Token);
                job.Summary = JsonSerializer.Serialize(summary, Json);
                final = JobStatus.Succeeded;
            }
            catch (OperationCanceledException) when (jobCts.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
            {
                final = JobStatus.Cancelled;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Job {JobId} ({Type}) failed", job.Id, job.Type);
                job.Error = ex.Message;
                final = JobStatus.Failed;
            }
            finally
            {
                _queue.MarkFinished(request.JobId);
            }

            job.Status = final;
            job.FinishedAt = _clock.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            _hub.Publish(new ProgressEvent(job.Id, job.Type, "finished", null, 0, 0, job.Error, final));
        }
    }
}
```

- [ ] **Step 7: Implement endpoints and scheduler**

`src/Spacearr/Jobs/JobEndpoints.cs`:

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Spacearr.Data;
using Spacearr.Data.Entities;

namespace Spacearr.Jobs;

public interface IJobFactories
{
    Func<IServiceProvider, IJob> Scan { get; }
    Func<IServiceProvider, IJob> Enrich { get; }
}

public sealed class JobFactories : IJobFactories
{
    public Func<IServiceProvider, IJob> Scan { get; init; } = _ => new NoOpJob(JobType.Scan);
    public Func<IServiceProvider, IJob> Enrich { get; init; } = _ => new NoOpJob(JobType.Enrich);
}

public sealed record JobResponse(int Id, JobType Type, JobStatus Status, JobTrigger Trigger, DateTime QueuedAt, DateTime? StartedAt, DateTime? FinishedAt, JobSummary? Summary, string? Error, ProgressEvent? Progress);
public sealed record PageResponse<T>(T[] Items, int Total, int Page, int PageSize);

public static class JobEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization();

        group.MapPost("/jobs/scan", async (IJobQueue queue, IJobFactories factories) =>
            Results.Accepted(null, new { jobId = await queue.EnqueueAsync(JobType.Scan, JobTrigger.Manual, factories.Scan) }));

        group.MapPost("/jobs/enrich", async (IJobQueue queue, IJobFactories factories) =>
            Results.Accepted(null, new { jobId = await queue.EnqueueAsync(JobType.Enrich, JobTrigger.Manual, factories.Enrich) }));

        group.MapGet("/jobs", async (SpacearrDb db, IProgressHub hub, int page = 1, int pageSize = 20) =>
        {
            page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
            var query = db.Jobs.AsNoTracking().OrderByDescending(j => j.Id);
            var total = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return Results.Ok(new PageResponse<JobResponse>(items.Select(j => ToResponse(j, hub)).ToArray(), total, page, pageSize));
        });

        group.MapGet("/jobs/{id:int}", async (int id, SpacearrDb db, IProgressHub hub) =>
        {
            var job = await db.Jobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id);
            return job is null ? Results.NotFound() : Results.Ok(ToResponse(job, hub));
        });

        group.MapPost("/jobs/{id:int}/cancel", (int id, IJobQueue queue) =>
            queue.TryCancel(id) ? Results.NoContent() : Results.Conflict(new { error = "Job is not running." }));

        group.MapGet("/events", async (HttpContext http, IProgressHub hub, CancellationToken ct) =>
        {
            http.Response.Headers.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-cache";
            http.Response.Headers["X-Accel-Buffering"] = "no";
            await http.Response.Body.FlushAsync(ct);

            var keepalive = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), ct);
                    await http.Response.WriteAsync(": keepalive\n\n", ct);
                    await http.Response.Body.FlushAsync(ct);
                }
            }, ct);

            try
            {
                await foreach (var e in hub.Subscribe(ct))
                {
                    await http.Response.WriteAsync($"event: {e.Kind}\ndata: {JsonSerializer.Serialize(e, Json)}\n\n", ct);
                    await http.Response.Body.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException) { }
        });

        return app;
    }

    private static JobResponse ToResponse(Job j, IProgressHub hub) => new(
        j.Id, j.Type, j.Status, j.Trigger, j.QueuedAt, j.StartedAt, j.FinishedAt,
        j.Summary is null ? null : JsonSerializer.Deserialize<JobSummary>(j.Summary, Json),
        j.Error,
        j.Status == JobStatus.Running ? hub.LastFor(j.Id) : null);
}
```

`src/Spacearr/Jobs/ScanScheduler.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Infrastructure;
using Spacearr.Settings;

namespace Spacearr.Jobs;

public sealed class ScanScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IJobQueue _queue;
    private readonly IJobFactories _factories;
    private readonly IClock _clock;
    private readonly ILogger<ScanScheduler> _log;
    private readonly TimeSpan _poll;

    public ScanScheduler(IServiceScopeFactory scopes, IJobQueue queue, IJobFactories factories, IClock clock, ILogger<ScanScheduler> log, IHostEnvironment env)
    {
        _scopes = scopes; _queue = queue; _factories = factories; _clock = clock; _log = log;
        _poll = env.IsEnvironment("Testing") ? TimeSpan.FromHours(24) : TimeSpan.FromMinutes(5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_poll);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await TickAsync(); }
            catch (Exception ex) { _log.LogWarning(ex, "Scheduler tick failed"); }
        }
    }

    internal async Task TickAsync()
    {
        using var scope = _scopes.CreateScope();
        var settings = await scope.ServiceProvider.GetRequiredService<ISettingsService>().GetAsync();
        if (settings.ScanIntervalHours <= 0) return;
        var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
        var last = await db.Jobs.Where(j => j.Type == JobType.Scan && j.FinishedAt != null)
            .OrderByDescending(j => j.FinishedAt).Select(j => j.FinishedAt).FirstOrDefaultAsync();
        if (last is null || _clock.UtcNow - last.Value >= TimeSpan.FromHours(settings.ScanIntervalHours))
            await _queue.EnqueueAsync(JobType.Scan, JobTrigger.Scheduled, _factories.Scan);
    }
}
```

- [ ] **Step 8: Register in Program.cs**

Before `var app = builder.Build();`:

```csharp
builder.Services.AddSingleton<Spacearr.Infrastructure.IClock, Spacearr.Infrastructure.SystemClock>();
builder.Services.AddSingleton<Spacearr.Jobs.IProgressHub, Spacearr.Jobs.ProgressHub>();
builder.Services.AddSingleton<Spacearr.Jobs.IJobQueue, Spacearr.Jobs.JobQueue>();
builder.Services.AddSingleton<Spacearr.Jobs.IJobFactories, Spacearr.Jobs.JobFactories>();
builder.Services.AddHostedService<Spacearr.Jobs.JobRunner>();
builder.Services.AddHostedService<Spacearr.Jobs.ScanScheduler>();
```

After `app.MapSettingsEndpoints();`: `app.MapJobEndpoints();`

On startup, mark any job left `Running` or `Queued` by a previous process as `Failed` with error "Interrupted by restart" (add to the migration block in `Program.cs`, after `SaveChangesAsync`):

```csharp
var stale = await db.Jobs.Where(j => j.Status == Spacearr.Data.Entities.JobStatus.Running || j.Status == Spacearr.Data.Entities.JobStatus.Queued).ToListAsync();
foreach (var s in stale) { s.Status = Spacearr.Data.Entities.JobStatus.Failed; s.Error = "Interrupted by restart"; s.FinishedAt = DateTime.UtcNow; }
if (stale.Count > 0) await db.SaveChangesAsync();
```

- [ ] **Step 9: Run tests**

Run: `dotnet test src/Spacearr.Tests --nologo -v q`
Expected: all pass (the gate test now also covers the job routes).

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "Add job queue, runner, progress hub, SSE events, job endpoints and scan scheduler

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01KCoYzAX74gBst65RVHz82j"
```

---

## Plan 1 completion check

- `dotnet build` and `dotnet test` pass with zero warnings.
- `dotnet run --project src/Spacearr` starts on 8787, creates `spacearr.db`, `secret.key`, `logs/` in the config dir, and `GET /api/v1/system/status` answers without auth while `GET /api/v1/jobs` answers 401.
- Continue with `docs/superpowers/plans/2026-09-10-02-scan-and-arr.md`.

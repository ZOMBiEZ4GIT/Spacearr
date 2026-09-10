using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Spacearr.Arr;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Infrastructure;
using Spacearr.Jobs;
using Spacearr.Scanning;

namespace Spacearr.Tests.Arr;

public class EnrichJobTests : IClassFixture<ArrTestApp>
{
    private readonly ArrTestApp _app;
    public EnrichJobTests(ArrTestApp app) => _app = app;

    [Fact]
    public async Task Matches_through_path_mapping_records_unmatched_and_removes_stale_items()
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
        var secrets = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        var root = new RootFolder { Path = "/mnt/movies" };
        db.RootFolders.Add(root);
        var inst = new ArrInstance { Type = ArrType.Radarr, Name = "R", BaseUrl = "http://radarr:7878", ApiKeyEncrypted = secrets.Protect("secret"), CreatedAt = DateTime.UtcNow,
            PathMappings = { new PathMapping { RemotePrefix = "/data/movies", LocalPrefix = "/mnt/movies" } } };
        db.ArrInstances.Add(inst);
        await db.SaveChangesAsync();
        db.MediaFiles.Add(new MediaFile { Path = "/mnt/movies/Film (2020)/film.mkv", RootFolderId = root.Id, SizeBytes = 1, ModifiedAt = DateTime.UtcNow, ScannedAt = DateTime.UtcNow });
        db.MediaItems.Add(new MediaItem { ArrInstanceId = inst.Id, ExternalId = 999, Kind = MediaKind.Movie, Title = "Stale", SyncedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var job = scope.ServiceProvider.GetRequiredService<EnrichJob>();
        var ctx = new JobContext(0, JobType.Enrich, scope.ServiceProvider, _app.Services.GetRequiredService<IProgressHub>());
        var (matched, unmatched, errors) = await job.RunAsync(ctx, CancellationToken.None);

        matched.Should().Be(1);
        unmatched.Should().Be(0);
        errors.Should().BeEmpty();

        var items = await db.MediaItems.AsNoTracking().Where(i => i.ArrInstanceId == inst.Id).ToListAsync();
        items.Should().ContainSingle();
        var film = items[0];
        film.ExternalId.Should().Be(10);
        film.Title.Should().Be("Film");
        film.MediaFileId.Should().NotBeNull();
        film.QualityName.Should().Be("Bluray-2160p");
        film.Tags.Should().Be("4k");
        film.PosterUrl.Should().StartWith("/MediaCover/");
        (await db.ArrInstances.AsNoTracking().SingleAsync(i => i.Id == inst.Id)).LastSyncError.Should().BeNull();
    }

    [Fact]
    public async Task Unreachable_instance_is_recorded_not_thrown()
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
        var secrets = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var inst = new ArrInstance { Type = ArrType.Sonarr, Name = "S", BaseUrl = "http://sonarr:8989", ApiKeyEncrypted = secrets.Protect("wrong"), CreatedAt = DateTime.UtcNow };
        db.ArrInstances.Add(inst);
        await db.SaveChangesAsync();

        var job = scope.ServiceProvider.GetRequiredService<EnrichJob>();
        var (_, _, errors) = await job.RunAsync(new JobContext(0, JobType.Enrich, scope.ServiceProvider, _app.Services.GetRequiredService<IProgressHub>()), CancellationToken.None);
        errors.Should().ContainSingle(e => e.Contains("S") && e.Contains("API key"));
        (await db.ArrInstances.AsNoTracking().SingleAsync(i => i.Id == inst.Id)).LastSyncError.Should().Contain("API key");
    }
}

using Microsoft.Extensions.DependencyInjection;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Infrastructure;
using Spacearr.Library;

namespace Spacearr.Tests.Library;

public static class Seed
{
    public static async Task<(int InstanceId, int[] ItemIds)> LibraryAsync(TestApp app, int movies = 6)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
        var secrets = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var root = new RootFolder { Path = "/mnt/movies" };
        db.RootFolders.Add(root);
        var inst = new ArrInstance { Type = ArrType.Radarr, Name = "Movies", BaseUrl = "http://radarr:7878", ApiKeyEncrypted = secrets.Protect("secret"), CreatedAt = DateTime.UtcNow };
        db.ArrInstances.Add(inst);
        await db.SaveChangesAsync();
        var ids = new List<int>();
        for (var i = 1; i <= movies; i++)
        {
            var bps = 2_000_000L * i; // increasing bitrate -> increasing heat
            // Path includes inst.Id so repeated Seed.LibraryAsync calls against the same
            // shared TestApp/db (multiple [Fact]s under one IClassFixture) never collide
            // on the unique MediaFiles.Path index.
            var file = new MediaFile { Path = $"/mnt/movies/inst{inst.Id}/M{i} (2020)/m{i}.mkv", RootFolderId = root.Id, SizeBytes = bps / 8 * 7200, ModifiedAt = DateTime.UtcNow, ScannedAt = DateTime.UtcNow,
                DurationSeconds = 7200, Width = 1920, Height = 1080, FrameRate = 24, VideoCodec = "h264", BitDepth = 8, VideoBitrateBps = bps, OverallBitrateBps = bps + 200_000, Container = "mkv", AudioSummary = "AAC 2.0" };
            db.MediaFiles.Add(file);
            await db.SaveChangesAsync();
            var item = new MediaItem { ArrInstanceId = inst.Id, ExternalId = 100 + i, Kind = MediaKind.Movie, Title = $"M{i}", Year = 2020, QualityProfileId = 5, QualityProfileName = "Ultra-HD", QualityName = i % 2 == 0 ? "Bluray-1080p" : "Bluray-2160p", Monitored = true, MediaFileId = file.Id, ArrFileId = 70 + i, ArrPath = $"/data/movies/M{i} (2020)/m{i}.mkv", TmdbId = 1000 + i, SyncedAt = DateTime.UtcNow };
            db.MediaItems.Add(item);
            await db.SaveChangesAsync();
            ids.Add(item.Id);
        }
        // Seeding writes library rows straight to the DB, bypassing the jobs that
        // normally invalidate the cached library queries - so invalidate here too,
        // exactly as a finished job would.
        scope.ServiceProvider.GetRequiredService<ILibraryCacheVersion>().Bump();
        return (inst.Id, ids.ToArray());
    }
}

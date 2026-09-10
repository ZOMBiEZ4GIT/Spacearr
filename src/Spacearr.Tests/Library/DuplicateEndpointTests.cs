using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Infrastructure;
using Spacearr.Tests.Arr;

namespace Spacearr.Tests.Library;

public class DuplicateEndpointTests : IClassFixture<ArrTestApp>
{
    private readonly ArrTestApp _app;
    public DuplicateEndpointTests(ArrTestApp app) => _app = app;

    [Fact]
    public async Task Shared_file_id_never_groups_and_genuine_duplicate_does()
    {
        var (instanceId, itemIds) = await Seed.LibraryAsync(_app);
        long m2Size;

        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
            var secrets = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

            var second = new ArrInstance { Type = ArrType.Radarr, Name = "Movies2", BaseUrl = "http://radarr2:7878", ApiKeyEncrypted = secrets.Protect("secret2"), CreatedAt = DateTime.UtcNow };
            db.ArrInstances.Add(second);
            await db.SaveChangesAsync();

            var m1 = await db.MediaItems.AsNoTracking().Include(i => i.MediaFile).SingleAsync(i => i.Id == itemIds[0]);
            var m2 = await db.MediaItems.AsNoTracking().Include(i => i.MediaFile).SingleAsync(i => i.Id == itemIds[1]);
            var rootId = await db.RootFolders.Select(r => r.Id).FirstAsync();
            m2Size = m2.MediaFile!.SizeBytes;

            // (a) Same physical file matched by a second arr instance (same MediaFileId,
            // same TmdbId as the original): must NOT be treated as a duplicate - and must
            // not crash the endpoint via a FileId collision in the heat lookup.
            db.MediaItems.Add(new MediaItem
            {
                ArrInstanceId = second.Id, ExternalId = 9001, Kind = MediaKind.Movie, Title = m1.Title, Year = m1.Year,
                QualityProfileId = 5, QualityProfileName = "Ultra-HD", QualityName = m1.QualityName, Monitored = true,
                MediaFileId = m1.MediaFileId, ArrFileId = m1.ArrFileId, ArrPath = m1.ArrPath, TmdbId = m1.TmdbId, SyncedAt = DateTime.UtcNow,
            });

            // (b) A genuine duplicate: a different physical file, matched on the second
            // instance, sharing the first instance's TmdbId for an existing movie.
            var dupFile = new MediaFile
            {
                Path = "/mnt/movies/inst2/M2-dup (2020)/m2dup.mkv", RootFolderId = rootId, SizeBytes = m2.MediaFile!.SizeBytes / 2,
                ModifiedAt = DateTime.UtcNow, ScannedAt = DateTime.UtcNow, DurationSeconds = 7200, Width = 1920, Height = 1080,
                FrameRate = 24, VideoCodec = "h264", BitDepth = 8, VideoBitrateBps = 1_000_000, OverallBitrateBps = 1_200_000, Container = "mkv", AudioSummary = "AAC 2.0",
            };
            db.MediaFiles.Add(dupFile);
            await db.SaveChangesAsync();

            db.MediaItems.Add(new MediaItem
            {
                ArrInstanceId = second.Id, ExternalId = 9002, Kind = MediaKind.Movie, Title = m2.Title, Year = m2.Year,
                QualityProfileId = 5, QualityProfileName = "Ultra-HD", QualityName = m2.QualityName, Monitored = true,
                MediaFileId = dupFile.Id, ArrFileId = 9002, ArrPath = "/data/movies/M2-dup (2020)/m2dup.mkv", TmdbId = m2.TmdbId, SyncedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var client = await AuthedClient.CreateAsync(_app);
        var response = await client.GetAsync("/api/v1/duplicates");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var groups = await response.Content.ReadFromJsonAsync<JsonElement>();
        var list = groups.EnumerateArray().ToList();
        list.Should().ContainSingle();

        var group = list[0];
        var members = group.GetProperty("members").EnumerateArray().ToList();
        members.Should().HaveCount(2);

        // The dup file is deliberately half the size of the original M2 file, so the
        // original is the largest member and the smaller (dup) size is what's wasted -
        // asserted against sizes known independently of the endpoint's own computation.
        var originalSize = m2Size;
        var dupSize = m2Size / 2;
        var total = originalSize + dupSize;
        group.GetProperty("wastedBytes").GetInt64().Should().Be(total - originalSize).And.Be(dupSize);

        var largestItemId = members.OrderByDescending(m => m.GetProperty("sizeBytes").GetInt64()).First().GetProperty("itemId").GetInt32();
        group.GetProperty("keepLargestId").GetInt32().Should().Be(largestItemId).And.Be(itemIds[1]);
    }
}

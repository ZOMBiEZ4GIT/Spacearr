using System.IO;
using Microsoft.EntityFrameworkCore;
using Spacearr.Data;
using Spacearr.Data.Entities;

namespace Spacearr.Library;

public sealed record LibraryRow(
    int ItemId, int InstanceId, string InstanceName, ArrType InstanceType, MediaKind Kind, string Title, int? Year,
    int? SeriesId, string? SeriesTitle, int? SeasonNumber, string? Episodes,
    string? QualityProfileName, int? QualityProfileId, string? QualityName, bool Monitored, string? Tags, string? PosterUrl,
    int FileId, string Path, long SizeBytes, double? DurationSeconds, int? Width, int? Height, double? FrameRate,
    string? VideoCodec, int? BitDepth, string? HdrFormat, long? VideoBitrateBps, long? OverallBitrateBps, string? AudioSummary, string? ProbeError,
    double? Nbpp)
{
    /// <summary>
    /// Buckets on max(Height, Width * 9 / 16) rather than raw Height, so wide/cinema
    /// masters (e.g. 1920x804, 3840x1600) that store a shorter-than-16:9 frame still
    /// bucket by their effective 16:9-equivalent height instead of the raw pixel height.
    /// </summary>
    public string? Resolution
    {
        get
        {
            double? fromHeight = Height;
            double? fromWidth = Width is null ? null : Width.Value * 9.0 / 16.0;
            var effective = fromHeight is null ? fromWidth : fromWidth is null ? fromHeight : Math.Max(fromHeight.Value, fromWidth.Value);
            if (effective is null) return null;
            var e = effective.Value;
            return e >= 2000 ? "2160p" : e >= 1300 ? "1440p" : e >= 1000 ? "1080p" : e >= 700 ? "720p" : e >= 500 ? "576p" : "480p";
        }
    }
}

public sealed record LibraryFilter(int? InstanceId, MediaKind? Kind, long MinBytes, string? Search);

public static class LibraryQueries
{
    public static async Task<List<LibraryRow>> RowsAsync(SpacearrDb db, LibraryFilter f, CancellationToken ct)
    {
        var items = db.MediaItems.AsNoTracking().Include(i => i.ArrInstance).Include(i => i.MediaFile).Where(i => i.MediaFileId != null);
        if (f.InstanceId is > 0) items = items.Where(i => i.ArrInstanceId == f.InstanceId);
        if (f.Kind is not null) items = items.Where(i => i.Kind == f.Kind);
        if (f.MinBytes > 0) items = items.Where(i => i.MediaFile!.SizeBytes >= f.MinBytes);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            items = items.Where(i => EF.Functions.Like(i.Title, $"%{s}%") || (i.SeriesTitle != null && EF.Functions.Like(i.SeriesTitle, $"%{s}%")));
        }
        var rows = (await items.ToListAsync(ct)).Select(i => FromItem(i, i.MediaFile!)).ToList();

        if (f.InstanceId is null or 0)
        {
            var matchedIds = db.MediaItems.Where(i => i.MediaFileId != null).Select(i => i.MediaFileId!.Value);
            var loose = db.MediaFiles.AsNoTracking().Where(m => !matchedIds.Contains(m.Id));
            if (f.MinBytes > 0) loose = loose.Where(m => m.SizeBytes >= f.MinBytes);
            if (!string.IsNullOrWhiteSpace(f.Search)) loose = loose.Where(m => EF.Functions.Like(m.Path, $"%{f.Search.Trim()}%"));
            if (f.Kind is null or MediaKind.Movie)
                rows.AddRange((await loose.ToListAsync(ct)).Select(FromLooseFile));
        }
        return rows;
    }

    private static LibraryRow FromItem(MediaItem i, MediaFile m) => new(
        i.Id, i.ArrInstanceId, i.ArrInstance?.Name ?? "", i.ArrInstance?.Type ?? ArrType.Radarr, i.Kind, i.Title, i.Year,
        i.SeriesId, i.SeriesTitle, i.SeasonNumber, i.EpisodeNumbers,
        i.QualityProfileName, i.QualityProfileId, i.QualityName, i.Monitored, i.Tags, i.PosterUrl,
        m.Id, m.Path, m.SizeBytes, m.DurationSeconds, m.Width, m.Height, m.FrameRate,
        m.VideoCodec, m.BitDepth, m.HdrFormat, m.VideoBitrateBps, m.OverallBitrateBps, m.AudioSummary, m.ProbeError,
        Heat.NormalisedBpp(m.VideoBitrateBps, m.Width, m.Height, m.FrameRate, m.VideoCodec));

    private static LibraryRow FromLooseFile(MediaFile m) => new(
        0, 0, "Unmatched", ArrType.Radarr, MediaKind.Movie, Path.GetFileName(m.Path), null,
        null, null, null, null, null, null, null, false, null, null,
        m.Id, m.Path, m.SizeBytes, m.DurationSeconds, m.Width, m.Height, m.FrameRate,
        m.VideoCodec, m.BitDepth, m.HdrFormat, m.VideoBitrateBps, m.OverallBitrateBps, m.AudioSummary, m.ProbeError,
        Heat.NormalisedBpp(m.VideoBitrateBps, m.Width, m.Height, m.FrameRate, m.VideoCodec));
}

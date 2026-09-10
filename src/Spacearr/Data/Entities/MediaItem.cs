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
    public string? EpisodeIds { get; set; }
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

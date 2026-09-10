using System.Net;
using Spacearr.Data.Entities;

namespace Spacearr.Arr;

public sealed record ArrStatus(string Version, string AppName);
public sealed record ArrProfile(int Id, string Name);
public sealed record ArrTag(int Id, string Label);
public sealed record ArrRootFolder(string Path);

public sealed record ArrItem(
    int ExternalId, MediaKind Kind, string Title, int? Year,
    int? SeriesId, string? SeriesTitle, int? SeasonNumber, int[] EpisodeNumbers, int[] EpisodeIds,
    int? QualityProfileId, string? QualityName, bool Monitored, int[] TagIds, string? PosterUrl,
    int? TmdbId, int? TvdbId, string? ImdbId, int? ArrFileId, string? ArrPath, long? ArrSizeBytes);

public sealed class ArrException : Exception
{
    public HttpStatusCode? Status { get; }
    public ArrException(HttpStatusCode? status, string message, Exception? inner = null) : base(message, inner) => Status = status;
}

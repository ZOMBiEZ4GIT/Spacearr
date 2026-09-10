using Spacearr.Data.Entities;

namespace Spacearr.Arr;

public interface IArrClient
{
    ArrType Type { get; }
    Task<ArrStatus> GetStatusAsync(CancellationToken ct);
    Task<IReadOnlyList<ArrProfile>> GetProfilesAsync(CancellationToken ct);
    Task<IReadOnlyList<ArrTag>> GetTagsAsync(CancellationToken ct);
    Task<IReadOnlyList<ArrRootFolder>> GetRootFoldersAsync(CancellationToken ct);
    Task<IReadOnlyList<ArrItem>> GetItemsAsync(CancellationToken ct);
    Task DeleteFileAsync(int arrFileId, CancellationToken ct);
    Task SetProfileAsync(int targetId, int profileId, CancellationToken ct);
    Task UnmonitorAsync(int targetId, CancellationToken ct);
    Task SearchAsync(int[] ids, CancellationToken ct);
}

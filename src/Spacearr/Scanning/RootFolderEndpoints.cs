using Microsoft.EntityFrameworkCore;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Settings;

namespace Spacearr.Scanning;

public sealed record RootFolderRequest(string Path, bool Enabled = true);
public sealed record RootFolderResponse(int Id, string Path, bool Enabled, DateTime? LastScanAt, bool Exists);
public sealed record ValidatePathRequest(string Path);
public sealed record ValidatePathResponse(bool Exists, string[] SampleFiles, int MediaFileCountSample);

public static class RootFolderEndpoints
{
    public static IEndpointRouteBuilder MapRootFolderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/roots").RequireAuthorization();

        group.MapGet("/", async (SpacearrDb db) =>
            Results.Ok((await db.RootFolders.AsNoTracking().OrderBy(r => r.Path).ToListAsync()).Select(ToResponse)));

        group.MapPost("/", async (RootFolderRequest req, SpacearrDb db) =>
        {
            if (!TryResolveAbsolutePath(req.Path, out var path, out var pathError)) return pathError!;
            if (!Directory.Exists(path)) return Results.BadRequest(new { error = "That folder does not exist or Spacearr cannot see it. Inside Docker, check the volume is mounted." });
            var all = await db.RootFolders.ToListAsync();
            if (all.Any(r => PathNormalizer.Equal(r.Path, path))) return Results.BadRequest(new { error = "That folder is already listed." });
            var root = new RootFolder { Path = path, Enabled = req.Enabled };
            db.RootFolders.Add(root);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/roots/{root.Id}", ToResponse(root));
        });

        group.MapPut("/{id:int}", async (int id, RootFolderRequest req, SpacearrDb db) =>
        {
            var root = await db.RootFolders.FindAsync(id);
            if (root is null) return Results.NotFound();
            if (!TryResolveAbsolutePath(req.Path, out var path, out var pathError)) return pathError!;
            if (!Directory.Exists(path)) return Results.BadRequest(new { error = "That folder does not exist or Spacearr cannot see it." });
            root.Path = path;
            root.Enabled = req.Enabled;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapDelete("/{id:int}", async (int id, SpacearrDb db) =>
        {
            var root = await db.RootFolders.FindAsync(id);
            if (root is null) return Results.NotFound();
            await db.MediaFiles.Where(f => f.RootFolderId == id).ExecuteDeleteAsync();
            db.RootFolders.Remove(root);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapPost("/validate", async (ValidatePathRequest req, IFileDiscovery discovery, ISettingsService settings) =>
        {
            if (!TryResolveAbsolutePath(req.Path, out var path, out var pathError)) return pathError!;
            if (!Directory.Exists(path)) return Results.Ok(new ValidatePathResponse(false, Array.Empty<string>(), 0));
            var ext = new HashSet<string>((await settings.GetAsync()).Extensions, StringComparer.OrdinalIgnoreCase);
            var sample = new List<string>();
            var count = 0;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                foreach (var f in discovery.Enumerate(path, ext, cts.Token))
                {
                    if (sample.Count < 5) sample.Add(f.Path);
                    if (++count >= 200) break;
                }
            }
            catch (OperationCanceledException) { }
            return Results.Ok(new ValidatePathResponse(true, sample.ToArray(), count));
        });

        return app;
    }

    private static RootFolderResponse ToResponse(RootFolder r) => new(r.Id, r.Path, r.Enabled, r.LastScanAt, Directory.Exists(r.Path));

    /// <summary>
    /// Rejects relative and traversal-only input (e.g. "..") up front via
    /// Path.IsPathRooted, then resolves the rest through Path.GetFullPath so any
    /// embedded ".." segments are collapsed before the path is persisted or used.
    /// </summary>
    private static bool TryResolveAbsolutePath(string? raw, out string resolved, out IResult? error)
    {
        var normalized = PathNormalizer.Normalize(raw ?? "");
        if (normalized.Length == 0 || !Path.IsPathRooted(normalized))
        {
            resolved = "";
            error = Results.BadRequest(new { error = "Enter an absolute path, e.g. /media/movies or D:\\Media." });
            return false;
        }
        try
        {
            resolved = PathNormalizer.Normalize(Path.GetFullPath(normalized));
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            resolved = "";
            error = Results.BadRequest(new { error = "Enter an absolute path, e.g. /media/movies or D:\\Media." });
            return false;
        }
    }
}

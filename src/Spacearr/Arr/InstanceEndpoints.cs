using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Infrastructure;
using Spacearr.Scanning;

namespace Spacearr.Arr;

public sealed record InstanceRequest(ArrType Type, string Name, string BaseUrl, string? ApiKey, bool Enabled = true);
public sealed record MappingRequest(string RemotePrefix, string LocalPrefix);
public sealed record MappingResponse(int Id, string RemotePrefix, string LocalPrefix);
public sealed record InstanceResponse(int Id, ArrType Type, string Name, string BaseUrl, bool Enabled, bool ApiKeySet, DateTime? LastSyncAt, string? LastSyncError, MappingResponse[] Mappings, int Matched, int Unmatched);
public sealed record TestRequest(ArrType Type, string BaseUrl, string ApiKey);
public sealed record TestResponse(bool Ok, string? Error, string? Version, string? AppName, string[] RootFolders, ArrProfile[] Profiles);
public sealed record MappingSuggestion(string RemotePrefix, string LocalPrefix, string Confidence);

public static class InstanceEndpoints
{
    public static IEndpointRouteBuilder MapInstanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/instances").RequireAuthorization();

        group.MapGet("/", async (SpacearrDb db) => Results.Ok(await ListAsync(db)));

        group.MapPost("/", async (InstanceRequest req, SpacearrDb db, ISecretProtector secrets) =>
        {
            var error = Validate(req, requireKey: true);
            if (error is not null) return Results.BadRequest(new { error });
            var inst = new ArrInstance { Type = req.Type, Name = req.Name.Trim(), BaseUrl = req.BaseUrl.Trim().TrimEnd('/'), ApiKeyEncrypted = secrets.Protect(req.ApiKey!.Trim()), Enabled = req.Enabled, CreatedAt = DateTime.UtcNow };
            db.ArrInstances.Add(inst);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/instances/{inst.Id}", (await ListAsync(db, inst.Id)).Single());
        });

        group.MapPut("/{id:int}", async (int id, InstanceRequest req, SpacearrDb db, ISecretProtector secrets, IMemoryCache cache) =>
        {
            var inst = await db.ArrInstances.FindAsync(id);
            if (inst is null) return Results.NotFound();
            var error = Validate(req, requireKey: false);
            if (error is not null) return Results.BadRequest(new { error });
            inst.Type = req.Type; inst.Name = req.Name.Trim(); inst.BaseUrl = req.BaseUrl.Trim().TrimEnd('/'); inst.Enabled = req.Enabled;
            if (!string.IsNullOrWhiteSpace(req.ApiKey)) inst.ApiKeyEncrypted = secrets.Protect(req.ApiKey.Trim());
            await db.SaveChangesAsync();
            cache.Remove($"profiles:{id}");
            return Results.NoContent();
        });

        group.MapDelete("/{id:int}", async (int id, SpacearrDb db, IMemoryCache cache) =>
        {
            var inst = await db.ArrInstances.FindAsync(id);
            if (inst is null) return Results.NotFound();
            db.ArrInstances.Remove(inst);
            await db.SaveChangesAsync();
            cache.Remove($"profiles:{id}");
            return Results.NoContent();
        });

        group.MapPost("/test", async (TestRequest req, IArrClientFactory factory, CancellationToken ct) =>
        {
            if (!IsHttpUrl(req.BaseUrl)) return Results.Ok(new TestResponse(false, "Enter a full URL such as http://radarr:7878. Inside Docker, use the container name rather than localhost.", null, null, Array.Empty<string>(), Array.Empty<ArrProfile>()));
            return Results.Ok(await TestAsync(factory.Create(req.Type, req.BaseUrl, req.ApiKey ?? ""), ct));
        });

        group.MapPost("/{id:int}/test", async (int id, SpacearrDb db, IArrClientFactory factory, CancellationToken ct) =>
        {
            var inst = await db.ArrInstances.FindAsync(id);
            return inst is null ? Results.NotFound() : Results.Ok(await TestAsync(factory.Create(inst), ct));
        });

        group.MapGet("/{id:int}/profiles", async (int id, SpacearrDb db, IArrClientFactory factory, IMemoryCache cache, CancellationToken ct) =>
        {
            var inst = await db.ArrInstances.FindAsync(id);
            if (inst is null) return Results.NotFound();
            var profiles = await cache.GetOrCreateAsync($"profiles:{id}", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                return await factory.Create(inst).GetProfilesAsync(ct);
            });
            return Results.Ok(profiles);
        });

        group.MapGet("/{id:int}/mappings", async (int id, SpacearrDb db) =>
            Results.Ok(await db.PathMappings.Where(m => m.ArrInstanceId == id).OrderByDescending(m => m.RemotePrefix.Length).Select(m => new MappingResponse(m.Id, m.RemotePrefix, m.LocalPrefix)).ToListAsync()));

        group.MapPost("/{id:int}/mappings", async (int id, MappingRequest req, SpacearrDb db) =>
        {
            if (await db.ArrInstances.FindAsync(id) is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(req.RemotePrefix) || string.IsNullOrWhiteSpace(req.LocalPrefix)) return Results.BadRequest(new { error = "Both paths are required." });
            var m = new PathMapping { ArrInstanceId = id, RemotePrefix = PathNormalizer.Normalize(req.RemotePrefix), LocalPrefix = PathNormalizer.Normalize(req.LocalPrefix) };
            db.PathMappings.Add(m);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/instances/{id}/mappings/{m.Id}", new MappingResponse(m.Id, m.RemotePrefix, m.LocalPrefix));
        });

        group.MapPut("/{id:int}/mappings/{mappingId:int}", async (int id, int mappingId, MappingRequest req, SpacearrDb db) =>
        {
            var m = await db.PathMappings.SingleOrDefaultAsync(x => x.Id == mappingId && x.ArrInstanceId == id);
            if (m is null) return Results.NotFound();
            m.RemotePrefix = PathNormalizer.Normalize(req.RemotePrefix); m.LocalPrefix = PathNormalizer.Normalize(req.LocalPrefix);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapDelete("/{id:int}/mappings/{mappingId:int}", async (int id, int mappingId, SpacearrDb db) =>
        {
            var m = await db.PathMappings.SingleOrDefaultAsync(x => x.Id == mappingId && x.ArrInstanceId == id);
            if (m is null) return Results.NotFound();
            db.PathMappings.Remove(m);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapPost("/{id:int}/mappings/suggest", async (int id, SpacearrDb db, IArrClientFactory factory, CancellationToken ct) =>
        {
            var inst = await db.ArrInstances.FindAsync(id);
            if (inst is null) return Results.NotFound();
            var arrRoots = await factory.Create(inst).GetRootFoldersAsync(ct);
            var localRoots = await db.RootFolders.Select(r => r.Path).ToListAsync(ct);
            var suggestions = new List<MappingSuggestion>();
            foreach (var arrRoot in arrRoots.Select(r => PathNormalizer.Normalize(r.Path)))
            {
                if (localRoots.Any(l => PathNormalizer.Equal(l, arrRoot))) continue; // same view of the disk, no mapping needed
                var leaf = arrRoot.Split('/').LastOrDefault(s => s.Length > 0);
                if (leaf is null) continue; // bare root ("/" or "C:/"), no leaf segment to match on
                var candidates = localRoots.Where(l => string.Equals(l.Split('/').LastOrDefault(s => s.Length > 0), leaf, StringComparison.OrdinalIgnoreCase)).ToList();
                if (candidates.Count == 0) continue;
                foreach (var c in candidates) suggestions.Add(new MappingSuggestion(arrRoot, c, candidates.Count == 1 ? "high" : "low"));
            }
            return Results.Ok(suggestions);
        });

        return app;
    }

    private static string? Validate(InstanceRequest req, bool requireKey)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return "Name is required.";
        if (!IsHttpUrl(req.BaseUrl)) return "Enter a full URL such as http://radarr:7878. Inside Docker, use the container name rather than localhost.";
        if (requireKey && string.IsNullOrWhiteSpace(req.ApiKey)) return "API key is required. Find it in the arr app under Settings > General > Security.";
        return null;
    }

    private static bool IsHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https");

    private static async Task<TestResponse> TestAsync(IArrClient client, CancellationToken ct)
    {
        try
        {
            var status = await client.GetStatusAsync(ct);
            var roots = await client.GetRootFoldersAsync(ct);
            var profiles = await client.GetProfilesAsync(ct);
            return new TestResponse(true, null, status.Version, status.AppName, roots.Select(r => r.Path).ToArray(), profiles.ToArray());
        }
        catch (ArrException ex)
        {
            return new TestResponse(false, ex.Message, null, null, Array.Empty<string>(), Array.Empty<ArrProfile>());
        }
    }

    private static async Task<List<InstanceResponse>> ListAsync(SpacearrDb db, int? onlyId = null)
    {
        var query = db.ArrInstances.AsNoTracking().Include(i => i.PathMappings).AsQueryable();
        if (onlyId is not null) query = query.Where(i => i.Id == onlyId);
        var instances = await query.OrderBy(i => i.Type).ThenBy(i => i.Name).ToListAsync();
        var counts = await db.MediaItems.GroupBy(i => i.ArrInstanceId)
            .Select(g => new { g.Key, Matched = g.Count(i => i.MediaFileId != null), Unmatched = g.Count(i => i.MediaFileId == null) })
            .ToDictionaryAsync(x => x.Key);
        return instances.Select(i =>
        {
            counts.TryGetValue(i.Id, out var c);
            return new InstanceResponse(i.Id, i.Type, i.Name, i.BaseUrl, i.Enabled, i.ApiKeyEncrypted.Length > 0, i.LastSyncAt, i.LastSyncError,
                i.PathMappings.OrderByDescending(m => m.RemotePrefix.Length).Select(m => new MappingResponse(m.Id, m.RemotePrefix, m.LocalPrefix)).ToArray(),
                c?.Matched ?? 0, c?.Unmatched ?? 0);
        }).ToList();
    }
}

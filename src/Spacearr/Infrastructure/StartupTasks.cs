using Microsoft.EntityFrameworkCore;
using Spacearr.Data;
using Spacearr.Data.Entities;

namespace Spacearr.Infrastructure;

public static class StartupTasks
{
    /// <summary>
    /// Brings the database up to date and guards against downgrades: applies
    /// pending migrations, refuses to start against a database written by a
    /// newer build, records the current version, and reconciles any job left
    /// Queued or Running by an unclean shutdown.
    /// </summary>
    public static async Task MigrateAndGuardAsync(IServiceProvider services, Version appVersion)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();

        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        await db.Database.MigrateAsync();

        var stored = await db.Settings.FindAsync("schema.appVersion");
        if (stored is not null && Version.TryParse(stored.Value, out var storedVersion) && storedVersion > appVersion)
        {
            throw new InvalidOperationException(
                $"Database was created by Spacearr {storedVersion}, newer than this build {appVersion}. Refusing to start.");
        }
        if (stored is null) db.Settings.Add(new Setting { Key = "schema.appVersion", Value = appVersion.ToString(3) });
        else stored.Value = appVersion.ToString(3);
        await db.SaveChangesAsync();

        var stale = await db.Jobs.Where(j => j.Status == JobStatus.Running || j.Status == JobStatus.Queued).ToListAsync();
        foreach (var s in stale)
        {
            s.Status = JobStatus.Failed;
            s.Error = "Interrupted by restart";
            s.FinishedAt = DateTime.UtcNow;
        }
        if (stale.Count > 0) await db.SaveChangesAsync();
    }
}

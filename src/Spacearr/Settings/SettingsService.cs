using Microsoft.EntityFrameworkCore;
using Spacearr.Data;
using Spacearr.Data.Entities;

namespace Spacearr.Settings;

public sealed record AppSettings(
    int ScanIntervalHours,
    string[] Extensions,
    string? FfprobePath,
    string? MediainfoPath,
    string HeatMode,
    string Theme)
{
    private static readonly string[] DefaultExtensionsSource = { ".mkv", ".mp4", ".avi", ".m4v", ".ts", ".mov", ".wmv", ".webm", ".mpg" };

    /// <summary>A fresh copy each call - callers must never be able to mutate the shared defaults.</summary>
    public static string[] DefaultExtensions => DefaultExtensionsSource.ToArray();
    public static AppSettings Defaults => new(6, DefaultExtensions, null, null, "relative", "dark");

    public string? Validate()
    {
        if (ScanIntervalHours < 0 || ScanIntervalHours > 168) return "Scan interval must be between 0 (disabled) and 168 hours.";
        if (Extensions is null || Extensions.Length == 0) return "At least one file extension is required.";
        if (Extensions.Any(e => string.IsNullOrWhiteSpace(e) || !e.StartsWith('.'))) return "Extensions must start with a dot, e.g. .mkv.";
        if (HeatMode is not ("relative" or "absolute")) return "Heat mode must be 'relative' or 'absolute'.";
        if (Theme is not ("dark" or "light")) return "Theme must be 'dark' or 'light'.";
        return null;
    }
}

public interface ISettingsService
{
    Task<AppSettings> GetAsync();
    Task SaveAsync(AppSettings settings);
}

public sealed class SettingsService : ISettingsService
{
    private readonly SpacearrDb _db;
    public SettingsService(SpacearrDb db) => _db = db;

    public async Task<AppSettings> GetAsync()
    {
        var rows = await _db.Settings.ToDictionaryAsync(s => s.Key, s => s.Value);
        var d = AppSettings.Defaults;
        return new AppSettings(
            rows.TryGetValue("scan.intervalHours", out var i) && int.TryParse(i, out var iv) ? iv : d.ScanIntervalHours,
            rows.TryGetValue("scan.extensions", out var e) ? e.Split(',', StringSplitOptions.RemoveEmptyEntries) : d.Extensions,
            NullIfEmpty(rows.GetValueOrDefault("tools.ffprobePath")),
            NullIfEmpty(rows.GetValueOrDefault("tools.mediainfoPath")),
            rows.GetValueOrDefault("heat.mode") ?? d.HeatMode,
            rows.GetValueOrDefault("ui.theme") ?? d.Theme);
    }

    public async Task SaveAsync(AppSettings s)
    {
        await Upsert("scan.intervalHours", s.ScanIntervalHours.ToString());
        await Upsert("scan.extensions", string.Join(',', s.Extensions.Select(x => x.Trim().ToLowerInvariant())));
        await Upsert("tools.ffprobePath", s.FfprobePath ?? "");
        await Upsert("tools.mediainfoPath", s.MediainfoPath ?? "");
        await Upsert("heat.mode", s.HeatMode);
        await Upsert("ui.theme", s.Theme);
        await _db.SaveChangesAsync();
    }

    private static string? NullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;

    private async Task Upsert(string key, string value)
    {
        var row = await _db.Settings.FindAsync(key);
        if (row is null) _db.Settings.Add(new Setting { Key = key, Value = value });
        else row.Value = value;
    }
}

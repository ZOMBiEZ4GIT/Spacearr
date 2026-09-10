using Spacearr.Settings;

namespace Spacearr.Scanning;

public interface IToolLocator
{
    string? Ffprobe { get; }
    string? Mediainfo { get; }
    Task RefreshAsync();
}

public sealed class ToolLocator : IToolLocator
{
    private readonly IServiceScopeFactory _scopes;
    public string? Ffprobe { get; private set; }
    public string? Mediainfo { get; private set; }

    public ToolLocator(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task RefreshAsync()
    {
        using var scope = _scopes.CreateScope();
        var settings = await scope.ServiceProvider.GetRequiredService<ISettingsService>().GetAsync();
        Ffprobe = Resolve(settings.FfprobePath, "ffprobe");
        Mediainfo = Resolve(settings.MediainfoPath, "mediainfo");
    }

    internal static string? Resolve(string? configured, string name)
    {
        if (!string.IsNullOrWhiteSpace(configured)) return File.Exists(configured) ? configured : null;
        var exe = OperatingSystem.IsWindows() ? name + ".exe" : name;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim('"'), exe);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}

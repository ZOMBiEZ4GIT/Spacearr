namespace Spacearr.Infrastructure;

public sealed class ConfigPaths
{
    public string Root { get; }
    public string DatabasePath => Path.Combine(Root, "spacearr.db");
    public string LogDirectory => Path.Combine(Root, "logs");
    public string SecretKeyPath => Path.Combine(Root, "secret.key");

    public ConfigPaths(string root)
    {
        Root = Path.GetFullPath(root);
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogDirectory);
    }

    public static string Resolve(IConfiguration configuration)
    {
        var fromEnv = configuration["SPACEARR_CONFIG_DIR"];
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;
        if (!OperatingSystem.IsWindows() && Directory.Exists("/config")) return "/config";
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spacearr");
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "spacearr");
    }
}

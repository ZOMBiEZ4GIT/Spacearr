using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Spacearr.Infrastructure;

namespace Spacearr.Tests.Infrastructure;

public class ConfigPathsTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void Explicit_config_dir_wins()
    {
        var wanted = Path.Combine(Path.GetTempPath(), "spacearr-explicit");
        ConfigPaths.Resolve(Config(("SPACEARR_CONFIG_DIR", wanted))).Should().Be(wanted);
    }

    [Fact]
    public void Blank_config_dir_falls_through_to_the_platform_default()
    {
        ConfigPaths.Resolve(Config(("SPACEARR_CONFIG_DIR", "   ")))
            .Should().NotBe("   ").And.NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void On_windows_the_default_is_under_local_app_data()
    {
        if (!OperatingSystem.IsWindows()) return;
        var resolved = ConfigPaths.Resolve(Config());
        resolved.Should().EndWith(@"\Spacearr");
        resolved.Should().StartWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    }

    [Fact]
    public void On_non_windows_without_a_config_mount_the_default_is_under_dot_config()
    {
        if (OperatingSystem.IsWindows()) return;
        if (Directory.Exists("/config")) return; // container default takes precedence by design
        ConfigPaths.Resolve(Config())
            .Should().Be(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "spacearr"));
    }

    [Fact]
    public void Constructor_creates_the_root_and_log_directories()
    {
        var root = Path.Combine(Path.GetTempPath(), "spacearr-paths", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new ConfigPaths(root);
            Directory.Exists(paths.Root).Should().BeTrue();
            Directory.Exists(paths.LogDirectory).Should().BeTrue();
            paths.DatabasePath.Should().Be(Path.Combine(paths.Root, "spacearr.db"));
            paths.SecretKeyPath.Should().Be(Path.Combine(paths.Root, "secret.key"));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* best effort */ }
        }
    }
}

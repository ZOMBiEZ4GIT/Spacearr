using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Spacearr.Auth;

namespace Spacearr.Tests.Auth;

public class AuthServiceExtensionsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "spacearr-authkeys-" + Guid.NewGuid().ToString("N"));
    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void Keys_directory_is_created()
    {
        new ServiceCollection().AddSpacearrDataProtection(_dir);
        Directory.Exists(Path.Combine(_dir, "keys")).Should().BeTrue();
    }

    [Fact]
    public void Keys_directory_is_owner_only_on_non_windows()
    {
        // The test host on CI (ubuntu-latest, see ci.yml) is where this assertion
        // actually runs; a Windows dev machine has no POSIX mode bits to check.
        if (OperatingSystem.IsWindows()) return;

        new ServiceCollection().AddSpacearrDataProtection(_dir);
        var mode = File.GetUnixFileMode(Path.Combine(_dir, "keys"));
        mode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}

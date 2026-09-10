using FluentAssertions;
using Spacearr.Infrastructure;

namespace Spacearr.Tests.Infrastructure;

public class SecretProtectorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "spacearr-secret-" + Guid.NewGuid().ToString("N"));
    public SecretProtectorTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void Round_trips_and_is_not_plaintext()
    {
        var p = new SecretProtector(new ConfigPaths(_dir));
        var cipher = p.Protect("abc123apikey");
        cipher.Should().NotContain("abc123apikey");
        p.Unprotect(cipher).Should().Be("abc123apikey");
    }

    [Fact]
    public void Key_file_is_created_once_and_reused()
    {
        var paths = new ConfigPaths(_dir);
        var cipher = new SecretProtector(paths).Protect("x");
        File.Exists(paths.SecretKeyPath).Should().BeTrue();
        new SecretProtector(paths).Unprotect(cipher).Should().Be("x");
    }

    [Fact]
    public void Same_plaintext_gives_different_ciphertext()
    {
        var p = new SecretProtector(new ConfigPaths(_dir));
        p.Protect("same").Should().NotBe(p.Protect("same"));
    }
}

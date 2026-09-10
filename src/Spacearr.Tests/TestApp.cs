using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Spacearr.Tests;

public sealed class TestApp : WebApplicationFactory<Program>
{
    public string ConfigDir { get; } = Path.Combine(Path.GetTempPath(), "spacearr-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(ConfigDir);
        builder.UseSetting("SPACEARR_CONFIG_DIR", ConfigDir);
        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(ConfigDir, recursive: true); } catch { /* best effort */ }
    }
}

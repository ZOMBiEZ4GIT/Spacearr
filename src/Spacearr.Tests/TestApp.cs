using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Spacearr.Tests;

public class TestApp : WebApplicationFactory<Program>
{
    public string ConfigDir { get; } = Path.Combine(Path.GetTempPath(), "spacearr-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(ConfigDir);
        builder.UseSetting("SPACEARR_CONFIG_DIR", ConfigDir);
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(ConfigureTestServices);
    }

    /// <summary>
    /// Override in a subclass to replace or add services after Program.cs has
    /// registered its own (e.g. swap in a fake IClock). No-op by default.
    /// </summary>
    protected virtual void ConfigureTestServices(IServiceCollection services) { }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(ConfigDir, recursive: true); } catch { /* best effort */ }
    }
}

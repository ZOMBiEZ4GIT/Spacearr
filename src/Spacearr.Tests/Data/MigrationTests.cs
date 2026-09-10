using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Spacearr.Data;

namespace Spacearr.Tests.Data;

public class MigrationTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public MigrationTests(TestApp app) => _app = app;

    [Fact]
    public async Task Fresh_database_has_all_tables_after_startup()
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
        var pending = await db.Database.GetPendingMigrationsAsync();
        pending.Should().BeEmpty();

        var tables = await db.Database
            .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'")
            .ToListAsync();
        tables.Should().Contain(new[] { "ArrInstances", "PathMappings", "RootFolders", "MediaFiles", "MediaItems", "Jobs", "ActionLogs", "Settings", "Users" });
    }

    [Fact]
    public async Task Database_uses_wal_journal_mode()
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
        var mode = await db.Database
            .SqlQueryRaw<string>("SELECT journal_mode AS Value FROM pragma_journal_mode")
            .ToListAsync();
        mode.Single().Should().Be("wal");
    }
}

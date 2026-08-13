using System;
using System.IO;
using AgentWire.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentWire.Tests.Fixtures;

public sealed class TestDbContextFactory : IDisposable
{
    private readonly string _tempDir;
    public AgentWireDbContext Db { get; }

    public TestDbContextFactory()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"agentwire-dbtest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var dbPath = Path.Combine(_tempDir, "test.db");

        var options = new DbContextOptionsBuilder<AgentWireDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        Db = new AgentWireDbContext(options);
        Db.Database.Migrate();
    }

    public void Dispose()
    {
        Db.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}

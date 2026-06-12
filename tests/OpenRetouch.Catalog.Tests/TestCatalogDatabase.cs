using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OpenRetouch.Catalog.Database;

namespace OpenRetouch.Catalog.Tests;

/// <summary>マイグレーション適用済みのテンポラリDBを提供するテストヘルパー。</summary>
public sealed class TestCatalogDatabase : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));

    public TestCatalogDatabase()
    {
        Directory.CreateDirectory(_root);
        ConnectionFactory = new ConnectionFactory(Path.Combine(_root, "catalog.db"));
        using var connection = ConnectionFactory.Open();
        new MigrationRunner(NullLogger<MigrationRunner>.Instance).Apply(connection);
    }

    public ConnectionFactory ConnectionFactory { get; }

    public string RootPath => _root;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

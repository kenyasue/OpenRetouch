using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OpenRetouch.Catalog.Database;
using Xunit;

namespace OpenRetouch.Catalog.Tests.Database;

public sealed class CatalogInitializerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));

    public CatalogInitializerTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task InitializeAsync_FreshDatabase_CreatesSchemaAndPassesIntegrityCheck()
    {
        var factory = new ConnectionFactory(Path.Combine(_root, "catalog.db"));
        var initializer = new CatalogInitializer(
            factory,
            new MigrationRunner(NullLogger<MigrationRunner>.Instance),
            NullLogger<CatalogInitializer>.Instance);

        await initializer.InitializeAsync();

        using var connection = factory.Open();
        MigrationRunner.GetUserVersion(connection).Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_IsIdempotent()
    {
        var factory = new ConnectionFactory(Path.Combine(_root, "catalog.db"));
        var initializer = new CatalogInitializer(
            factory,
            new MigrationRunner(NullLogger<MigrationRunner>.Instance),
            NullLogger<CatalogInitializer>.Instance);

        await initializer.InitializeAsync();
        var act = () => initializer.InitializeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeAsync_NotADatabaseFile_Throws()
    {
        var dbPath = Path.Combine(_root, "catalog.db");
        await File.WriteAllTextAsync(dbPath, "this is not a sqlite database file at all");
        var factory = new ConnectionFactory(dbPath);
        var initializer = new CatalogInitializer(
            factory,
            new MigrationRunner(NullLogger<MigrationRunner>.Instance),
            NullLogger<CatalogInitializer>.Instance);

        var act = () => initializer.InitializeAsync();

        // 非DBファイルはSQLite接続/クエリの段階で失敗する
        await act.Should().ThrowAsync<SqliteException>();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

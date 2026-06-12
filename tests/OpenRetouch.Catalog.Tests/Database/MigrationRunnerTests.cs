using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OpenRetouch.Catalog.Database;
using Xunit;

namespace OpenRetouch.Catalog.Tests.Database;

public sealed class MigrationRunnerTests : IDisposable
{
    private static readonly string[] ExpectedTables =
    [
        "folders", "photos", "edits", "albums", "photo_album_map",
        "presets", "export_jobs", "export_job_items", "thumbnail_cache",
    ];

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));
    private readonly ConnectionFactory _factory;
    private readonly MigrationRunner _runner = new(NullLogger<MigrationRunner>.Instance);

    public MigrationRunnerTests()
    {
        Directory.CreateDirectory(_root);
        _factory = new ConnectionFactory(Path.Combine(_root, "catalog.db"));
    }

    [Fact]
    public void Apply_EmptyDatabase_CreatesAllTables()
    {
        using var connection = _factory.Open();

        _runner.Apply(connection);

        var tables = QueryTableNames(connection);
        tables.Should().Contain(ExpectedTables);
    }

    [Fact]
    public void Apply_EmptyDatabase_SetsUserVersion()
    {
        using var connection = _factory.Open();

        _runner.Apply(connection);

        MigrationRunner.GetUserVersion(connection).Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Apply_CalledTwice_IsIdempotent()
    {
        using var connection = _factory.Open();

        _runner.Apply(connection);
        var versionAfterFirst = MigrationRunner.GetUserVersion(connection);

        var act = () => _runner.Apply(connection);

        act.Should().NotThrow();
        MigrationRunner.GetUserVersion(connection).Should().Be(versionAfterFirst);
    }

    [Fact]
    public void Apply_CreatesExpectedIndexes()
    {
        using var connection = _factory.Open();

        _runner.Apply(connection);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index' AND name LIKE 'idx_%';";
        var indexes = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            indexes.Add(reader.GetString(0));
        }

        indexes.Should().Contain(["idx_photos_captured_at", "idx_photos_rating", "idx_photos_folder", "idx_edits_photo"]);
    }

    [Fact]
    public void LoadMigrations_ReturnsOrderedMigrations()
    {
        var migrations = MigrationRunner.LoadMigrations();

        migrations.Should().NotBeEmpty();
        migrations.Select(m => m.Version).Should().BeInAscendingOrder();
        migrations[0].Version.Should().Be(1);
    }

    private static List<string> QueryTableNames(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
        var tables = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
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

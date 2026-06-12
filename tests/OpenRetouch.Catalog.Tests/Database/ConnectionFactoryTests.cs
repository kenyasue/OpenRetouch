using FluentAssertions;
using Microsoft.Data.Sqlite;
using OpenRetouch.Catalog.Database;
using Xunit;

namespace OpenRetouch.Catalog.Tests.Database;

public sealed class ConnectionFactoryTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));

    public ConnectionFactoryTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Open_CreatesDatabaseFile()
    {
        var dbPath = Path.Combine(_root, "catalog.db");
        var factory = new ConnectionFactory(dbPath);

        using (factory.Open())
        {
        }

        File.Exists(dbPath).Should().BeTrue();
    }

    [Fact]
    public void Open_EnablesWalMode()
    {
        var factory = new ConnectionFactory(Path.Combine(_root, "catalog.db"));

        using var connection = factory.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        var mode = command.ExecuteScalar() as string;

        mode.Should().Be("wal");
    }

    [Fact]
    public void Open_EnablesForeignKeys()
    {
        var factory = new ConnectionFactory(Path.Combine(_root, "catalog.db"));

        using var connection = factory.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys;";
        var enabled = Convert.ToInt32(command.ExecuteScalar());

        enabled.Should().Be(1);
    }

    [Fact]
    public void Constructor_EmptyPath_Throws()
    {
        var act = () => new ConnectionFactory("");
        act.Should().Throw<ArgumentException>();
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

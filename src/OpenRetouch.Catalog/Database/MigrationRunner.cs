using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace OpenRetouch.Catalog.Database;

/// <summary>
/// PRAGMA user_version ベースのスキーママイグレーション。
/// 埋め込みリソース(Database/Migrations/M*.sql)を連番順に適用する。
/// </summary>
public sealed partial class MigrationRunner
{
    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(ILogger<MigrationRunner> logger)
    {
        _logger = logger;
    }

    /// <summary>未適用のマイグレーションをすべて適用する。冪等。</summary>
    public void Apply(SqliteConnection connection)
    {
        var currentVersion = GetUserVersion(connection);
        var migrations = LoadMigrations();

        foreach (var (version, name, sql) in migrations.Where(m => m.Version > currentVersion))
        {
            _logger.LogInformation("Applying migration {Name} (v{Version})", name, version);

            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.ExecuteNonQuery();

            // PRAGMAはパラメータ化できないため数値を直接埋め込む(versionはファイル名由来の整数のみ)
            command.CommandText = $"PRAGMA user_version = {version};";
            command.ExecuteNonQuery();

            transaction.Commit();
        }
    }

    public static int GetUserVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>埋め込みリソースからマイグレーション一覧を連番順に読み込む。</summary>
    internal static IReadOnlyList<(int Version, string Name, string Sql)> LoadMigrations()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var migrations = new List<(int Version, string Name, string Sql)>();

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            var match = MigrationResourcePattern().Match(resourceName);
            if (!match.Success)
            {
                continue;
            }

            var version = int.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture);
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Failed to load migration resource: {resourceName}");
            using var reader = new StreamReader(stream);
            migrations.Add((version, resourceName, reader.ReadToEnd()));
        }

        return migrations.OrderBy(m => m.Version).ToList();
    }

    [GeneratedRegex(@"\.M(?<version>\d{3})_[^.]+\.sql$")]
    private static partial Regex MigrationResourcePattern();
}

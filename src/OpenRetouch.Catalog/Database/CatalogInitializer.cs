using Microsoft.Extensions.Logging;
using OpenRetouch.Core.Abstractions;

namespace OpenRetouch.Catalog.Database;

/// <summary>
/// 起動時のカタログDB初期化(作成・マイグレーション・整合性チェック)。
/// </summary>
public sealed class CatalogInitializer : ICatalogInitializer
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly MigrationRunner _migrationRunner;
    private readonly ILogger<CatalogInitializer> _logger;

    public CatalogInitializer(
        ConnectionFactory connectionFactory,
        MigrationRunner migrationRunner,
        ILogger<CatalogInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _migrationRunner = migrationRunner;
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken ct = default)
    {
        // SQLite操作は同期APIのためワーカースレッドで実行し、UIスレッドをブロックしない
        return Task.Run(() =>
        {
            using var connection = _connectionFactory.Open();

            _migrationRunner.Apply(connection);

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            var result = command.ExecuteScalar() as string;
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Catalog integrity check failed: {Result}", result);
                throw new CatalogCorruptedException(
                    $"Catalog database failed integrity check: {result}");
            }

            _logger.LogInformation(
                "Catalog initialized: {Path} (schema v{Version})",
                _connectionFactory.DatabasePath,
                MigrationRunner.GetUserVersion(connection));
        }, ct);
    }
}

using Microsoft.Data.Sqlite;
using OpenRetouch.Core.Environment;

namespace OpenRetouch.Catalog.Database;

/// <summary>
/// カタログDBへのSQLite接続を生成する。
/// すべての接続でWAL・外部キー・synchronous=NORMALを適用する。
/// </summary>
public sealed class ConnectionFactory
{
    private readonly string _databasePath;

    public ConnectionFactory(IAppEnvironment environment)
        : this(environment.CatalogDatabasePath)
    {
    }

    public ConnectionFactory(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = databasePath;
    }

    public string DatabasePath => _databasePath;

    /// <summary>PRAGMA適用済みのオープン済み接続を返す。呼び出し側がDisposeする。</summary>
    public SqliteConnection Open()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        try
        {
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
            command.ExecuteNonQuery();

            return connection;
        }
        catch
        {
            // PRAGMA失敗時(破損ファイル等)に接続をリークさせない
            connection.Dispose();
            throw;
        }
    }
}

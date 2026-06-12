namespace OpenRetouch.Core.Abstractions;

/// <summary>
/// カタログDB(SQLite)の起動時初期化。実装はCatalogレイヤーが提供する(依存性逆転)。
/// </summary>
public interface ICatalogInitializer
{
    /// <summary>
    /// DBファイルの作成・PRAGMA適用・マイグレーション・整合性チェックを行う。
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);
}

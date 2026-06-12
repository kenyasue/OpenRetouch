namespace OpenRetouch.Core.Abstractions.Repositories;

/// <summary>thumbnail_cacheテーブルへのアクセス。実装はCatalogレイヤー。</summary>
public interface IThumbnailCacheRepository
{
    /// <summary>photoId→サムネイルパスの全マッピングを返す。</summary>
    Task<IReadOnlyDictionary<string, string>> GetAllThumbPathsAsync(CancellationToken ct = default);

    /// <summary>サムネイルキャッシュ情報を登録/更新する。</summary>
    Task UpsertAsync(
        string photoId,
        string thumbPath,
        DateTimeOffset sourceModifiedAt,
        CancellationToken ct = default);

    /// <summary>キャッシュ情報を削除する(再生成のため)。</summary>
    Task RemoveAsync(IReadOnlyList<string> photoIds, CancellationToken ct = default);

    /// <summary>全キャッシュ行を削除する(キャッシュクリア用)。</summary>
    Task RemoveAllAsync(CancellationToken ct = default);

    /// <summary>サムネイルキャッシュ行を持たない写真の件数を返す(起動時チェック用)。</summary>
    Task<long> CountPhotosWithoutThumbnailAsync(CancellationToken ct = default);
}

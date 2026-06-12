using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Abstractions.Repositories;

/// <summary>photosテーブルへのアクセス。実装はCatalogレイヤー。</summary>
public interface IPhotoRepository
{
    /// <summary>条件に一致する写真を取得する。</summary>
    Task<IReadOnlyList<Photo>> QueryAsync(PhotoQuery query, CancellationToken ct = default);

    /// <summary>IDリストに一致する写真を取得する(順序は保証しない)。</summary>
    Task<IReadOnlyList<Photo>> GetByIdsAsync(IReadOnlyList<string> photoIds, CancellationToken ct = default);

    /// <summary>登録済みの全file_pathを返す(重複検出用)。</summary>
    Task<HashSet<string>> GetExistingFilePathsAsync(CancellationToken ct = default);

    /// <summary>写真をバッチ登録する(トランザクション、既存パスは無視)。</summary>
    Task InsertBatchAsync(IReadOnlyList<Photo> photos, CancellationToken ct = default);

    /// <summary>複数写真の星評価を一括更新する。</summary>
    Task UpdateRatingAsync(IReadOnlyList<string> photoIds, int rating, CancellationToken ct = default);

    /// <summary>複数写真のフラグを一括更新する。</summary>
    Task UpdateFlagAsync(IReadOnlyList<string> photoIds, PhotoFlag flag, CancellationToken ct = default);

    /// <summary>複数写真の色ラベルを一括更新する。</summary>
    Task UpdateColorLabelAsync(IReadOnlyList<string> photoIds, ColorLabel label, CancellationToken ct = default);
}

using OpenRetouch.Core.Editing;

namespace OpenRetouch.Core.Abstractions.Repositories;

/// <summary>editsテーブルへのアクセス。実装はCatalogレイヤー。</summary>
public interface IEditRepository
{
    /// <summary>写真の現行編集を取得する(未編集ならnull)。</summary>
    Task<EditSettings?> GetCurrentAsync(string photoId, CancellationToken ct = default);

    /// <summary>写真の現行編集を登録/更新する。</summary>
    Task UpsertCurrentAsync(string photoId, EditSettings settings, CancellationToken ct = default);

    /// <summary>編集レコードを持つ写真IDの集合を返す。</summary>
    Task<IReadOnlyCollection<string>> GetEditedPhotoIdsAsync(CancellationToken ct = default);
}

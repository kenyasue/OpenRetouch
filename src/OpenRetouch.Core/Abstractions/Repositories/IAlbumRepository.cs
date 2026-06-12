using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Abstractions.Repositories;

/// <summary>albums / photo_album_map テーブルへのアクセス。実装はCatalogレイヤー。</summary>
public interface IAlbumRepository
{
    Task<IReadOnlyList<Album>> GetAllAsync(CancellationToken ct = default);

    Task<Album> InsertAsync(string name, CancellationToken ct = default);

    /// <summary>アルバムを削除する(photo_album_mapの所属情報も削除。写真自体は削除しない)。</summary>
    Task DeleteAsync(string albumId, CancellationToken ct = default);

    /// <summary>写真をアルバムに追加する(既に所属している写真は無視)。</summary>
    Task AddPhotosAsync(string albumId, IReadOnlyList<string> photoIds, CancellationToken ct = default);

    Task RemovePhotosAsync(string albumId, IReadOnlyList<string> photoIds, CancellationToken ct = default);
}

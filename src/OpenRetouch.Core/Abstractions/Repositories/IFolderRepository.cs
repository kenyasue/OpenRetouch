using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Abstractions.Repositories;

/// <summary>foldersテーブルへのアクセス。実装はCatalogレイヤー。</summary>
public interface IFolderRepository
{
    Task<IReadOnlyList<Folder>> GetAllAsync(CancellationToken ct = default);

    Task<Folder?> GetByPathAsync(string path, CancellationToken ct = default);

    Task InsertAsync(Folder folder, CancellationToken ct = default);

    /// <summary>
    /// フォルダとその写真に紐づく全行(編集・サムネイルキャッシュ・アルバム所属・書き出し履歴)を
    /// 1トランザクションで削除する。削除したサムネイルファイルのパス一覧を返す(ファイル自体は削除しない)。
    /// </summary>
    Task<IReadOnlyList<string>> DeleteCascadeAsync(string folderId, CancellationToken ct = default);
}

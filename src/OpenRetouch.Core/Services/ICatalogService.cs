using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Services;

/// <summary>サムネイル生成完了の通知。</summary>
public sealed record ThumbnailReadyEventArgs(string PhotoId, string ThumbnailPath);

/// <summary>カタログの読み取り・選別・アルバム操作とサムネイル生成の調停。</summary>
public interface ICatalogService
{
    /// <summary>条件に一致する写真一覧を取得する。</summary>
    Task<IReadOnlyList<Photo>> QueryPhotosAsync(PhotoQuery query, CancellationToken ct = default);

    /// <summary>photoId→サムネイルパスのマッピングを返す。</summary>
    Task<IReadOnlyDictionary<string, string>> GetThumbnailPathsAsync(CancellationToken ct = default);

    /// <summary>未生成サムネイルの一括生成ジョブを投入する。</summary>
    void EnqueueThumbnailGeneration();

    /// <summary>
    /// サムネイル未生成(キャッシュ行なし、またはファイル消失)の写真があれば
    /// 一括生成ジョブを投入する(起動時の自動再開用)。0件のときは投入しない。
    /// </summary>
    Task EnqueueThumbnailGenerationIfMissingAsync(CancellationToken ct = default);

    /// <summary>
    /// 指定写真のサムネイルを無効化して再生成する(編集内容の反映用)。
    /// </summary>
    Task RefreshThumbnailsAsync(IReadOnlyList<string> photoIds, CancellationToken ct = default);

    /// <summary>
    /// 全サムネイルキャッシュを破棄してファイルを削除し、全写真分の再生成ジョブを投入する。
    /// </summary>
    Task ClearThumbnailCacheAsync(CancellationToken ct = default);

    /// <summary>サムネイル1件生成ごとに発火(ワーカースレッドから)。</summary>
    event EventHandler<ThumbnailReadyEventArgs>? ThumbnailReady;

    // ---- 選別 ----

    /// <summary>複数写真の星評価(0-5)を一括設定する。範囲外は例外。</summary>
    Task SetRatingAsync(IReadOnlyList<string> photoIds, int rating, CancellationToken ct = default);

    /// <summary>複数写真の採用/不採用フラグを一括設定する(Noneで解除)。</summary>
    Task SetFlagAsync(IReadOnlyList<string> photoIds, PhotoFlag flag, CancellationToken ct = default);

    /// <summary>複数写真の色ラベルを一括設定する(Noneで解除)。</summary>
    Task SetColorLabelAsync(IReadOnlyList<string> photoIds, ColorLabel label, CancellationToken ct = default);

    // ---- フォルダ / アルバム ----

    Task<IReadOnlyList<Folder>> GetFoldersAsync(CancellationToken ct = default);

    /// <summary>
    /// フォルダとその写真をカタログから削除する(登録解除)。
    /// 写真・編集・サムネイルキャッシュ・アルバム所属・書き出し履歴の行とサムネイルファイルを削除するが、
    /// 元の画像ファイルとXMPサイドカーは削除しない。
    /// </summary>
    Task RemoveFolderFromCatalogAsync(string folderId, CancellationToken ct = default);

    Task<IReadOnlyList<Album>> GetAlbumsAsync(CancellationToken ct = default);

    Task<Album> CreateAlbumAsync(string name, CancellationToken ct = default);

    Task DeleteAlbumAsync(string albumId, CancellationToken ct = default);

    Task AddToAlbumAsync(string albumId, IReadOnlyList<string> photoIds, CancellationToken ct = default);

    Task RemoveFromAlbumAsync(string albumId, IReadOnlyList<string> photoIds, CancellationToken ct = default);
}

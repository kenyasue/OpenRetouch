using OpenRetouch.Core.Editing;

namespace OpenRetouch.Core.Services;

/// <summary>非破壊編集の取得・保存。</summary>
public interface IEditService
{
    /// <summary>写真の現行編集を取得する(未編集・破損時はデフォルト)。</summary>
    Task<EditSettings> GetEditAsync(string photoId, CancellationToken ct = default);

    /// <summary>写真の現行編集を保存する(デバウンスは呼び出し側の責務)。</summary>
    Task SaveEditAsync(string photoId, EditSettings settings, CancellationToken ct = default);

    /// <summary>編集をデフォルトに戻して保存する。</summary>
    Task ResetAsync(string photoId, CancellationToken ct = default);

    /// <summary>編集コピー&ペースト用のバッファ(メモリ上、セッション内)。</summary>
    EditSettings? CopyBuffer { get; set; }

    /// <summary>
    /// 編集設定(Basic+Crop全体)を複数写真へ上書き適用する。戻り値は成功枚数。
    /// </summary>
    Task<int> ApplyToPhotosAsync(
        EditSettings settings, IReadOnlyList<string> photoIds, CancellationToken ct = default);
}

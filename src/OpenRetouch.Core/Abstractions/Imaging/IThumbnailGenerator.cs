using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Abstractions.Imaging;

/// <summary>サムネイル生成。実装はImagingレイヤー。</summary>
public interface IThumbnailGenerator
{
    /// <summary>
    /// 現行の編集(クロップ+調整)を適用した長辺320px相当のサムネイルJPEGを生成し、生成先パスを返す。
    /// デコード失敗時は例外を投げる(呼び出し側がスキップ判断する)。
    /// </summary>
    Task<string> GenerateAsync(Photo photo, EditSettings edit, CancellationToken ct = default);
}

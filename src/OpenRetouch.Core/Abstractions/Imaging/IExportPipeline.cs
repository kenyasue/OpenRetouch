using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Export;
using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Abstractions.Imaging;

/// <summary>書き出しパイプライン。実装はImagingレイヤー。</summary>
public interface IExportPipeline
{
    /// <summary>
    /// 元画像をフル解像度でデコードし、編集(クロップ+調整)を適用、
    /// リサイズ・エンコードして outputPath に保存する。
    /// プレビューと同一の変換実装を使う(WYSIWYG保証)。
    /// 出力先が元画像と同一パスの場合は例外(非破壊保証)。
    /// </summary>
    Task ExportAsync(
        Photo photo,
        EditSettings edit,
        ExportSettings settings,
        string outputPath,
        CancellationToken ct = default);
}

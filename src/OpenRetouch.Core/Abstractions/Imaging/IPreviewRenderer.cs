using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Abstractions.Imaging;

/// <summary>レンダリング結果(BGRA8、行パディングなし)。</summary>
public sealed record RenderedImage(byte[] PixelsBgra, int Width, int Height);

/// <summary>編集プレビューのレンダリング。実装はImagingレイヤー。</summary>
public interface IPreviewRenderer
{
    /// <summary>
    /// 写真のドラフトプレビュー(長辺1280px・Orientation適用済み)に編集を適用して返す。
    /// デコード結果は内部キャッシュされ、スライダー操作時は調整適用のみ行われる。
    /// </summary>
    /// <param name="applyCrop">falseの場合クロップを適用しない(クロップ編集UI用)。</param>
    Task<RenderedImage> RenderAsync(
        Photo photo, EditSettings settings, bool applyCrop = true, CancellationToken ct = default);
}

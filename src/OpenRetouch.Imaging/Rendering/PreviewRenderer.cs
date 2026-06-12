using Microsoft.Extensions.Logging;
using OpenRetouch.Core.Abstractions.Imaging;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Models;
using OpenRetouch.Imaging.Raw;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace OpenRetouch.Imaging.Rendering;

/// <summary>
/// 編集プレビューのレンダリング。
/// デコード済みベース画像(長辺1280px)を直近2枚LRUキャッシュし、
/// スライダー操作時はAdjustmentPipelineの適用のみ行う。
/// JPEG/PNG/TIFFはWIC、RAWはLibRaw(ハーフサイズ現像)でベース画像を生成する。
/// RAWのプレビューは書き出しと同一の現像パラメータ(カメラWB/色行列/sRGB)を使うが、
/// halfSizeのため補間精度はフル現像より低い(輝度・色傾向は一致、ピクセル単位の厳密一致ではない)。
/// </summary>
public sealed class PreviewRenderer : IPreviewRenderer, IDisposable
{
    /// <summary>ドラフトプレビューの長辺(px)。</summary>
    public const int DraftLongEdge = 1280;

    private const int CacheCapacity = 2;
    private const long MaxPixelCount = 200_000_000;

    private readonly LibRawDecoder _rawDecoder;
    private readonly ILogger<PreviewRenderer> _logger;
    private readonly SemaphoreSlim _decodeGate = new(1, 1);
    private readonly LinkedList<(string PhotoId, RenderedImage Base)> _baseCache = [];

    public PreviewRenderer(LibRawDecoder rawDecoder, ILogger<PreviewRenderer> logger)
    {
        _rawDecoder = rawDecoder;
        _logger = logger;
    }

    public async Task<RenderedImage> RenderAsync(
        Photo photo,
        EditSettings settings,
        bool applyCrop = true,
        CancellationToken ct = default)
    {
        var baseImage = await GetOrDecodeBaseAsync(photo, ct);
        ct.ThrowIfCancellationRequested();

        // ベースは共有キャッシュのためコピーしてから変換・調整を適用する。
        // CPU負荷の高い処理はワーカースレッドで実行する(UIスレッドから直接await可能)。
        return await Task.Run(
            () =>
            {
                var buffer = new byte[baseImage.PixelsBgra.Length];
                baseImage.PixelsBgra.CopyTo(buffer, 0);
                var image = new RenderedImage(buffer, baseImage.Width, baseImage.Height);

                if (applyCrop)
                {
                    image = GeometryTransform.Apply(settings.Crop, image);
                }

                AdjustmentPipeline.Apply(settings.Basic, image.PixelsBgra, image.Width, image.Height);
                return image;
            },
            ct);
    }

    private async Task<RenderedImage> GetOrDecodeBaseAsync(Photo photo, CancellationToken ct)
    {
        lock (_baseCache)
        {
            var node = _baseCache.First;
            while (node is not null)
            {
                if (node.Value.PhotoId == photo.Id)
                {
                    _baseCache.Remove(node);
                    _baseCache.AddFirst(node);
                    return node.Value.Base;
                }

                node = node.Next;
            }
        }

        // デコードは直列化(同一写真の二重デコード防止+メモリ抑制)
        await _decodeGate.WaitAsync(ct);
        try
        {
            lock (_baseCache)
            {
                // ダブルチェック(ゲート待機中に他スレッドがデコード済みの場合)
                var node = _baseCache.First;
                while (node is not null)
                {
                    if (node.Value.PhotoId == photo.Id)
                    {
                        return node.Value.Base;
                    }

                    node = node.Next;
                }
            }

            // RAWはハーフサイズ現像(書き出しと同じ色再現=WYSIWYG)、それ以外はWIC
            var decoded = RawFileTypes.IsRaw(photo.FileExtension)
                ? await _rawDecoder.DecodeDevelopedPreviewAsync(photo.FilePath, DraftLongEdge, ct)
                : await DecodeAsync(photo.FilePath, ct);

            lock (_baseCache)
            {
                _baseCache.AddFirst((photo.Id, decoded));
                while (_baseCache.Count > CacheCapacity)
                {
                    _baseCache.RemoveLast();
                }
            }

            return decoded;
        }
        finally
        {
            _decodeGate.Release();
        }
    }

    private async Task<RenderedImage> DecodeAsync(string filePath, CancellationToken ct)
    {
        _logger.LogDebug("Decoding preview base: {Path}", filePath);

        var file = await StorageFile.GetFileFromPathAsync(filePath);
        using var stream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(stream);

        if ((long)decoder.PixelWidth * decoder.PixelHeight > MaxPixelCount)
        {
            throw new InvalidOperationException($"Image too large to decode: {filePath}");
        }

        // 回転画像のスケール座標系補正込みの共通デコード
        return await WicDecoding.DecodeScaledAsync(decoder, DraftLongEdge, ct);
    }

    public void Dispose() => _decodeGate.Dispose();
}

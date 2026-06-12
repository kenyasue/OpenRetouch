using Microsoft.Extensions.Logging;
using OpenRetouch.Core.Abstractions.Imaging;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Models;

namespace OpenRetouch.Imaging.Rendering;

/// <inheritdoc cref="IAutoToneService"/>
public sealed class AutoToneService : IAutoToneService
{
    private readonly IPreviewRenderer _renderer;
    private readonly ILogger<AutoToneService> _logger;

    public AutoToneService(IPreviewRenderer renderer, ILogger<AutoToneService> logger)
    {
        _renderer = renderer;
        _logger = logger;
    }

    public async Task<BasicAdjustments> ComputeAsync(Photo photo, CancellationToken ct = default)
    {
        // 編集なしのドラフトプレビュー(長辺1280px)を解析対象にする
        // (PreviewRendererのデコードキャッシュに乗るため、Edit画面からの実行は追加デコード不要)
        var rendered = await _renderer.RenderAsync(photo, new EditSettings(), ct: ct);
        var histogram = BuildLuminanceHistogram(rendered.PixelsBgra);
        var result = AutoToneCalculator.Calculate(histogram);
        _logger.LogInformation(
            "Auto tone computed for {Path}: exposure={Exposure}, contrast={Contrast}",
            photo.FilePath, result.Exposure, result.Contrast);
        return result;
    }

    /// <summary>BGRA8ピクセルからRec.601輝度の256binヒストグラムを構築する。</summary>
    internal static long[] BuildLuminanceHistogram(byte[] pixelsBgra)
    {
        var histogram = new long[256];
        for (var i = 0; i + 3 < pixelsBgra.Length; i += 4)
        {
            var luminance =
                (29 * pixelsBgra[i] + 150 * pixelsBgra[i + 1] + 77 * pixelsBgra[i + 2]) >> 8;
            histogram[luminance]++;
        }

        return histogram;
    }
}

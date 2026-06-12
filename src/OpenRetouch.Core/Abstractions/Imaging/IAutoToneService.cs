using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Abstractions.Imaging;

/// <summary>自動トーン補正(Auto Tone)の解析。実装はImagingレイヤー。</summary>
public interface IAutoToneService
{
    /// <summary>
    /// 写真を解析して自動トーン補正値を返す。
    /// 補正対象はトーン6項目(Exposure/Contrast/Highlights/Shadows/Whites/Blacks)のみで、
    /// 色・ディテール項目は常にデフォルト(0)。
    /// </summary>
    Task<BasicAdjustments> ComputeAsync(Photo photo, CancellationToken ct = default);
}

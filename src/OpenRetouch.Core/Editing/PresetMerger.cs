using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Editing;

/// <summary>
/// プリセット適用のマージ規則:
/// プリセットに含まれる基本調整のみ上書きし、クロップ等は維持する(機能設計書準拠)。
/// </summary>
public static class PresetMerger
{
    public static EditSettings Merge(EditSettings current, Preset preset)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(preset);

        return new EditSettings
        {
            Version = current.Version,
            Basic = preset.Settings.Clone(),
            Crop = current.Crop.Clone(),
        };
    }
}

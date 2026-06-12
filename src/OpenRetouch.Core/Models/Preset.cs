using OpenRetouch.Core.Editing;

namespace OpenRetouch.Core.Models;

/// <summary>編集プリセット(presetsテーブルに対応)。基本調整のみを保持し、クロップは含めない。</summary>
public sealed class Preset
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Category { get; init; }

    public required BasicAdjustments Settings { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

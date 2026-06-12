namespace OpenRetouch.Core.Models;

/// <summary>アルバム(コレクション)。フォルダとは独立した写真の論理グルーピング。</summary>
public sealed class Album
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public int SortOrder { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

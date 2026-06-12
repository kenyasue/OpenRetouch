namespace OpenRetouch.Core.Models;

/// <summary>取り込み元フォルダ(foldersテーブルに対応)。</summary>
public sealed class Folder
{
    public required string Id { get; init; }

    /// <summary>フォルダの絶対パス(UNIQUE)。</summary>
    public required string Path { get; init; }

    public required string Name { get; init; }

    /// <summary>フォルダツリー用の親ID(M1ではnull固定)。</summary>
    public string? ParentId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

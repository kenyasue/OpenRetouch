namespace OpenRetouch.Catalog.Database;

/// <summary>カタログDBの整合性チェック失敗を表す。</summary>
public sealed class CatalogCorruptedException : Exception
{
    public CatalogCorruptedException(string message)
        : base(message)
    {
    }

    public CatalogCorruptedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

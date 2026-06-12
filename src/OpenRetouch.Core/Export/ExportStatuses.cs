namespace OpenRetouch.Core.Export;

/// <summary>export_jobs.status の値。</summary>
public static class ExportJobStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

/// <summary>export_job_items.status の値。</summary>
public static class ExportItemStatus
{
    public const string Pending = "pending";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
}

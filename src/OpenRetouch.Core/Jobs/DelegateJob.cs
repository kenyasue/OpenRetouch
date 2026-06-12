namespace OpenRetouch.Core.Jobs;

/// <summary>デリゲートで本体を定義する汎用ジョブ。</summary>
public sealed class DelegateJob : IJob
{
    private readonly Func<IProgress<(int Done, int Total)>, CancellationToken, Task> _execute;

    public DelegateJob(
        string displayName,
        Func<IProgress<(int Done, int Total)>, CancellationToken, Task> execute)
    {
        DisplayName = displayName;
        _execute = execute;
    }

    public string Id { get; } = Guid.NewGuid().ToString();

    public string DisplayName { get; }

    public Task ExecuteAsync(IProgress<(int Done, int Total)> progress, CancellationToken ct) =>
        _execute(progress, ct);
}

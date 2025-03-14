namespace PointlessWaymarks.CommonTools;

public record TaggedDebouncedDelayedRequest
{
    public DateTimeOffset CreatedOn { get; init; } = DateTimeOffset.Now;
    public string Source { get; init; } = string.Empty;
    public string Tag { get; init; } = string.Empty;
    public Func<string, Task> TaskFunc { get; init; } = _ => Task.CompletedTask;
}
using System.Collections.Concurrent;
using System.Diagnostics;
using Serilog;

namespace PointlessWaymarks.CommonTools;

/// <summary>
///     This class is a one-at-a-time processor that debounces requests. One use for this class is for 'noisy'
///     UI events such as tracking user selection changes where you don't actually want to do anything until
///     the selection seems to be 'fully finished' and where displaying/adding results with one-at-a-time
///     processing matches expectations.
/// </summary>
public class TaggedDebouncedDelayedTaskQueue
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _debounceTokens = new();
    private readonly BlockingCollection<(string tag, Func<Task> taskFunc)> _jobs = new();
    private readonly List<(string tag, Func<Task>)> _pausedQueue = new();
    private bool _suspended;

    public TaggedDebouncedDelayedTaskQueue(bool suspended = false)
    {
        _suspended = suspended;
        var thread = new Thread(OnStart) { IsBackground = true };
        thread.Start();
    }

    public int DebounceMilliseconds { get; set; } = 350;

    public void Enqueue((string tag, Func<Task> taskFunc) job)
    {
        if (_suspended)
        {
            _pausedQueue.Add(job);
        }
        else
        {
            if (_debounceTokens.TryGetValue(job.tag, out var existingToken)) existingToken.Cancel();

            var cts = new CancellationTokenSource();
            _debounceTokens[job.tag] = cts;

            Task.Delay(DebounceMilliseconds, cts.Token).ContinueWith(t =>
            {
                try
                {
                    if (!t.IsCanceled)
                    {
                        _jobs.Add(job);
                        _debounceTokens.TryRemove(job.tag, out _);
                    }
                }
                finally
                {
                    try
                    {
                        cts.Dispose();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }, TaskScheduler.Default);
        }
    }

    private void OnStart()
    {
        foreach (var job in _jobs.GetConsumingEnumerable(CancellationToken.None))
            try
            {
                job.taskFunc.Invoke().Wait();
            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
                Log.Error(e, "WorkQueue Error");
            }
    }

    public void Suspend(bool suspend)
    {
        _suspended = suspend;
        if (!_suspended && _pausedQueue.Any())
        {
            _pausedQueue.ForEach(Enqueue);
            _pausedQueue.Clear();
        }
    }
}
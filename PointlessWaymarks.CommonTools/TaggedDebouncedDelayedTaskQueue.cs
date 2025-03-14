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
    private readonly ConcurrentDictionary<string, DateTimeOffset> _debounceTokens = new();
    private readonly List<TaggedDebouncedDelayedRequest> _pausedQueue = new();
    private readonly BlockingCollection<TaggedDebouncedDelayedRequest> _requests = new();
    private bool _suspended;

    public TaggedDebouncedDelayedTaskQueue(bool suspended = false)
    {
        _suspended = suspended;
        var requestThread = new Thread(OnRequestQueueStart) { IsBackground = true };
        requestThread.Start();
    }

    public LimitedConcurrentQueue<string> DebouncedRecord { get; set; } = new(255);

    public int DebounceMilliseconds { get; set; } = 350;

    public LimitedConcurrentQueue<string> RunRecord { get; set; } = new(255);

    public void Enqueue(TaggedDebouncedDelayedRequest job)
    {
        if (_suspended)
            _pausedQueue.Add(job);
        else
            _requests.Add(job);
    }

    private void ScheduleJob(int millisecondsDelay, TaggedDebouncedDelayedRequest job)
    {
        try
        {
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(millisecondsDelay);
                    var runTime = DateTimeOffset.Now;
                    await job.TaskFunc.Invoke($"{job.Source} - Created {job.CreatedOn}, Run {runTime}");
                    RunRecord.Enqueue($"{job.Source} - Created {job.CreatedOn}, Run {runTime}");
                }
                catch (Exception e)
                {
                    Debug.Print(e.Message);
                    Log.Error(e, "ScheduleJob Task.Run Inner Error");
                }
            });
        }
        catch (Exception e)
        {
            Debug.Print(e.Message);
            Log.Error(e, "ScheduleJob Task Run Outer Error");
        }
    }

    private void OnRequestQueueStart()
    {
        foreach (var job in _requests.GetConsumingEnumerable(CancellationToken.None))
            try
            {
                var runTime = DateTimeOffset.Now;

                if (_debounceTokens.TryGetValue(job.Tag, out var existingTime))
                {
                    if (existingTime > runTime)
                    {
                        DebouncedRecord.Enqueue(
                            $"{job.Source} - Requested {job.CreatedOn}, Debounced {runTime} ");
                        continue;
                    }

                    ScheduleJob(DebounceMilliseconds, job);
                    _debounceTokens[job.Tag] = runTime.AddMilliseconds(DebounceMilliseconds);
                }
                else
                {
                    ScheduleJob(DebounceMilliseconds, job);
                    _debounceTokens.TryAdd(job.Tag, runTime.AddMilliseconds(DebounceMilliseconds));
                }
            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
                Log.Error(e, "OnRequestQueueStart Error");
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
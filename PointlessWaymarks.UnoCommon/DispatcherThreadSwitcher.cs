using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;

namespace PointlessWaymarks.UnoCommon;

public struct DispatcherThreadSwitcher : INotifyCompletion, ICriticalNotifyCompletion
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueuePriority _priority;

    public DispatcherThreadSwitcher(DispatcherQueue dispatcherQueue, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        ArgumentNullException.ThrowIfNull(dispatcherQueue);
        _dispatcherQueue = dispatcherQueue;
        _priority = priority;
    }

    public bool IsCompleted => _dispatcherQueue.HasThreadAccess;

    public DispatcherThreadSwitcher GetAwaiter()
    {
        return this;
    }

    public void GetResult()
    {
    }

    public void OnCompleted(Action continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        _dispatcherQueue.TryEnqueue(_priority, new DispatcherQueueHandler(continuation));
    }

    public void UnsafeOnCompleted(Action continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        _dispatcherQueue.TryEnqueue(_priority, new DispatcherQueueHandler(continuation));
    }
}

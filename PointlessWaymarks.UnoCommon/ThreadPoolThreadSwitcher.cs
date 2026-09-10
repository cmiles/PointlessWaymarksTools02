using System.Runtime.CompilerServices;

namespace PointlessWaymarks.UnoCommon;

public struct ThreadPoolThreadSwitcher : INotifyCompletion, ICriticalNotifyCompletion
{
    public bool IsCompleted => SynchronizationContext.Current == null;

    public ThreadPoolThreadSwitcher GetAwaiter()
    {
        return this;
    }

    public void GetResult()
    {
    }

    public void OnCompleted(Action continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        ThreadPool.QueueUserWorkItem(static state => ((Action)state!)(), continuation);
    }

    public void UnsafeOnCompleted(Action continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        ThreadPool.UnsafeQueueUserWorkItem(static state => ((Action)state!)(), continuation);
    }
}

using Microsoft.UI.Dispatching;

namespace PointlessWaymarks.UnoCommon;

public class ThreadSwitcher
{
    /// <summary>
    ///     If present the PinnedDispatcher will be used by ResumeForegroundAsync() (otherwise DispatcherQueue.GetForCurrentThread()
    ///     is used)
    /// </summary>
    public static DispatcherQueue? PinnedDispatcher { get; set; }

    /// <summary>
    ///     Alias for <see cref="PinnedDispatcher"/> conforming to WinUI DispatcherQueue naming.
    /// </summary>
    public static DispatcherQueue? PinnedDispatcherQueue
    {
        get => PinnedDispatcher;
        set => PinnedDispatcher = value;
    }

    public static ThreadPoolThreadSwitcher ResumeBackgroundAsync()
    {
        return new();
    }

    /// <summary>
    ///     Uses the PinnedDispatcher if not null, otherwise falls back to DispatcherQueue.GetForCurrentThread().
    /// </summary>
    /// <param name="priority">The priority at which to enqueue on the DispatcherQueue.</param>
    /// <returns>A DispatcherThreadSwitcher awaitable instance.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when PinnedDispatcher is null and no DispatcherQueue is available on the current thread.
    /// </exception>
    public static DispatcherThreadSwitcher ResumeForegroundAsync(DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        var dispatcher = PinnedDispatcher ?? DispatcherQueue.GetForCurrentThread();

        if (dispatcher == null)
            throw new InvalidOperationException(
                "Cannot resume on foreground thread: PinnedDispatcher is null and no DispatcherQueue was found for the current thread. Ensure ThreadSwitcher.PinnedDispatcher is initialized on the UI thread at application startup.");

        PinnedDispatcher ??= dispatcher;

        return new DispatcherThreadSwitcher(dispatcher, priority);
    }

    /// <summary>
    ///     Switches execution to the specified DispatcherQueue.
    /// </summary>
    /// <param name="dispatcherQueue">The target DispatcherQueue.</param>
    /// <param name="priority">The priority at which to enqueue on the DispatcherQueue.</param>
    /// <returns>A DispatcherThreadSwitcher awaitable instance.</returns>
    public static DispatcherThreadSwitcher ResumeForegroundAsync(DispatcherQueue dispatcherQueue, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        ArgumentNullException.ThrowIfNull(dispatcherQueue);
        return new DispatcherThreadSwitcher(dispatcherQueue, priority);
    }
}

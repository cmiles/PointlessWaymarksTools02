using System.Collections.Concurrent;

namespace PointlessWaymarks.CommonTools;

public class LimitedConcurrentQueue<T>(int limit) : ConcurrentQueue<T>
{
    public readonly int Limit = limit;

    public new void Enqueue(T element)
    {
        base.Enqueue(element);
        if (Count > Limit) TryDequeue(out var discard);
    }
}
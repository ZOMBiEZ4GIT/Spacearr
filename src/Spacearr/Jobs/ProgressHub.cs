using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Spacearr.Jobs;

public interface IProgressHub
{
    void Publish(ProgressEvent e);
    IAsyncEnumerable<ProgressEvent> Subscribe(CancellationToken ct);
    ProgressEvent? LastFor(int jobId);
}

public sealed class ProgressHub : IProgressHub
{
    private readonly ConcurrentDictionary<Guid, Channel<ProgressEvent>> _subscribers = new();
    private readonly ConcurrentDictionary<int, ProgressEvent> _last = new();

    public void Publish(ProgressEvent e)
    {
        _last[e.JobId] = e;
        foreach (var channel in _subscribers.Values)
            channel.Writer.TryWrite(e);
    }

    public ProgressEvent? LastFor(int jobId) => _last.TryGetValue(jobId, out var e) ? e : null;

    public IAsyncEnumerable<ProgressEvent> Subscribe(CancellationToken ct)
    {
        // The channel is created and registered eagerly, before the iterator
        // is first moved, so events published between Subscribe() returning
        // and the caller's first MoveNextAsync() are buffered rather than lost.
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<ProgressEvent>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.DropOldest });
        _subscribers[id] = channel;
        return Drain(id, channel, ct);
    }

    private async IAsyncEnumerable<ProgressEvent> Drain(Guid id, Channel<ProgressEvent> channel, [EnumeratorCancellation] CancellationToken ct)
    {
        try
        {
            await foreach (var e in channel.Reader.ReadAllAsync(ct))
                yield return e;
        }
        finally
        {
            _subscribers.TryRemove(id, out _);
        }
    }
}

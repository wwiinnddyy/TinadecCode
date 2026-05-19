using System.Collections.Concurrent;
using System.Threading.Channels;
using Tinadec.Contracts.Events;

namespace Tinadec.AgentCore.Services;

public sealed class EventHub
{
    private readonly ConcurrentDictionary<Guid, Channel<EventEnvelope>> _subscribers = new();

    public void Publish(EventEnvelope envelope)
    {
        foreach (var subscriber in _subscribers.Values)
        {
            subscriber.Writer.TryWrite(envelope);
        }
    }

    public async IAsyncEnumerable<EventEnvelope> Subscribe(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<EventEnvelope>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _subscribers[id] = channel;

        try
        {
            await foreach (var envelope in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return envelope;
            }
        }
        finally
        {
            _subscribers.TryRemove(id, out _);
        }
    }
}

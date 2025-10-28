using System.Linq;
using IPTVGuideDog.Core.Channels;

namespace IPTVGuideDog.SocketHost.Channels;

internal sealed class InMemoryChannelCatalog : IChannelCatalog
{
    public Task<IReadOnlyCollection<ChannelGroup>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Replace with provider-backed lookup and caching strategy.
        IReadOnlyCollection<ChannelGroup> groups = Array.Empty<ChannelGroup>();
        return Task.FromResult(groups);
    }

    public IAsyncEnumerable<ChannelDescriptor> StreamChannelUpdatesAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Surface real-time channel updates from provider polling.
        return AsyncEnumerable.Empty<ChannelDescriptor>();
    }
}

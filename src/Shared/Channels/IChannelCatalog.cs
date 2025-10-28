namespace IPTVGuideDog.Domain.Channels;

public interface IChannelCatalog
{
    Task<IReadOnlyCollection<ChannelGroup>> GetGroupsAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChannelDescriptor> StreamChannelUpdatesAsync(CancellationToken cancellationToken = default);
}

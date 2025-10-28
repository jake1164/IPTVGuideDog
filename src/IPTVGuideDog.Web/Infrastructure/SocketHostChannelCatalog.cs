using IPTVGuideDog.Core.Channels;
using IPTVGuideDog.Web.Configuration;
using Microsoft.Extensions.Options;

namespace IPTVGuideDog.Web.Infrastructure;

internal sealed class SocketHostChannelCatalog : IChannelCatalog
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<SocketHostOptions> _options;

    public SocketHostChannelCatalog(HttpClient httpClient, IOptions<SocketHostOptions> options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public Task<IReadOnlyCollection<ChannelGroup>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Replace with call to the socket host discovery endpoint.
        IReadOnlyCollection<ChannelGroup> groups = Array.Empty<ChannelGroup>();
        return Task.FromResult(groups);
    }

    public IAsyncEnumerable<ChannelDescriptor> StreamChannelUpdatesAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Replace with streaming API once available.
        return AsyncEnumerable.Empty<ChannelDescriptor>();
    }
}

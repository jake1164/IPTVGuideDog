namespace IPTVGuideDog.Domain.Channels;

public record ChannelGroup(string Name, IReadOnlyCollection<ChannelDescriptor> Channels);

namespace IPTVGuideDog.Core.Channels;

public record ChannelGroup(string Name, IReadOnlyCollection<ChannelDescriptor> Channels);

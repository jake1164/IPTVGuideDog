namespace IPTVGuideDog.Core.Channels;

public record ChannelDescriptor(string ChannelId, string Name, Uri StreamUri, string? GroupName);

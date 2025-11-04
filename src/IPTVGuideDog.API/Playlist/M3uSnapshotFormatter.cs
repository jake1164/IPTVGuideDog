using System.Text;
using IPTVGuideDog.Core.Channels;

namespace IPTVGuideDog.SocketHost.Playlist;

internal static class M3uSnapshotFormatter
{
    public static string From(IEnumerable<ChannelGroup> groups)
    {
        var builder = new StringBuilder();
        builder.AppendLine("#EXTM3U");

        foreach (var group in groups)
        {
            foreach (var channel in group.Channels)
            {
                builder.AppendLine($"#EXTINF:-1 group-title=\"{group.Name}\",{channel.Name}");
                builder.AppendLine(channel.StreamUri.ToString());
            }
        }

        return builder.ToString();
    }
}

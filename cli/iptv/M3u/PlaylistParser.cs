namespace Iptv.Cli.M3u;

public sealed class PlaylistParser
{
    public PlaylistDocument Parse(string content)
    {
        if (content is null)
        {
            throw new CliException("Playlist content was null.", ExitCodes.ParseError);
        }

        var lines = SplitLines(content);
        var preamble = new List<string>();
        var entries = new List<M3uEntry>();

        var index = 0;
        while (index < lines.Length)
        {
            var line = lines[index];
            if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                var metadata = new List<string> { line };
                index++;

                while (index < lines.Length && lines[index].StartsWith("#") && !lines[index].StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
                {
                    metadata.Add(lines[index]);
                    index++;
                }

                while (index < lines.Length && string.IsNullOrWhiteSpace(lines[index]))
                {
                    metadata.Add(lines[index]);
                    index++;
                }

                string? url = null;
                if (index < lines.Length)
                {
                    url = lines[index].Trim();
                    index++;
                }

                entries.Add(new M3uEntry(metadata, url));
            }
            else
            {
                if (entries.Count == 0)
                {
                    preamble.Add(line);
                }
                index++;
            }
        }

        return new PlaylistDocument(preamble, entries);
    }

    private static string[] SplitLines(string content)
        => content.Replace("\r\n", "\n", StringComparison.Ordinal)
                   .Replace('\r', '\n')
                   .Split('\n');
}

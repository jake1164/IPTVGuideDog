using Iptv.Cli.IO;
using Iptv.Cli.M3u;
using Iptv.Cli.Net;

namespace Iptv.Cli.Commands;

public sealed class GroupsCommand
{
    private static readonly string[] TemplateHeader =
    [
        "######  This is a DROP list. Put a '#' in front of any group you want to KEEP. ######",
        "######  Lines without '#' will be DROPPED. Blank lines are ignored.              ######",
        string.Empty
    ];

    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;
    private readonly TextWriter _diagnostics;
    private readonly HttpClient _httpClient;
    private readonly PlaylistParser _parser;

    public GroupsCommand(
        TextWriter stdout,
        TextWriter stderr,
        TextWriter diagnostics,
        HttpClient httpClient,
        PlaylistParser parser)
    {
        _stdout = stdout;
        _stderr = stderr;
        _diagnostics = diagnostics;
        _httpClient = httpClient;
        _parser = parser;
    }

    public async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(context.PlaylistSource))
        {
            throw new CliException("Missing required: --playlist-url or --config with playlist", ExitCodes.ConfigError);
        }

        var fetcher = new SourceFetcher(_httpClient, _diagnostics);
        var playlistContent = await fetcher.GetStringAsync(context.PlaylistSource, cancellationToken);
        var document = _parser.Parse(playlistContent);

        IEnumerable<M3uEntry> entries = document.Entries.Where(e => !string.IsNullOrEmpty(e.Url));
        if (context.LiveOnly)
        {
            entries = entries.Where(e => LiveClassifier.IsLive(e.Url));
        }

        var groups = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (!string.IsNullOrEmpty(entry.Group))
            {
                groups.Add(entry.Group);
            }
        }

        if (_diagnostics != TextWriter.Null)
        {
            await _diagnostics.WriteLineAsync($"Discovered {groups.Count} groups.");
        }

        var outputLines = TemplateHeader.Concat(groups).ToList();
        var outPath = context.OutputPath;
        if (string.IsNullOrEmpty(outPath) || outPath == "-")
        {
            foreach (var line in outputLines)
            {
                await _stdout.WriteLineAsync(line);
            }
        }
        else
        {
            await TextFileWriter.WriteAtomicAsync(outPath, outputLines, cancellationToken);
            await _stdout.WriteLineAsync($"Groups written to {outPath}");
        }

        return ExitCodes.Success;
    }
}

using Iptv.Cli.IO;
using Iptv.Cli.M3u;
using Iptv.Cli.Net;

namespace Iptv.Cli.Commands;

public sealed class RunCommand
{
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;
    private readonly TextWriter _diagnostics;
    private readonly HttpClient _httpClient;
    private readonly PlaylistParser _parser;

    public RunCommand(
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

        var epgRequested = !string.IsNullOrEmpty(context.EpgSource);
        var playlistOut = context.PlaylistOutputPath;
        var epgOut = context.EpgOutputPath;

        if (epgRequested && string.IsNullOrEmpty(playlistOut) && string.IsNullOrEmpty(epgOut))
        {
            throw new CliException("When an EPG is requested you must provide --out-playlist, --out-epg, or use '-' for stdout.", ExitCodes.ConfigError);
        }

        var fetcher = new SourceFetcher(_httpClient, _diagnostics);
        var playlistContent = await fetcher.GetStringAsync(context.PlaylistSource, cancellationToken);
        var document = _parser.Parse(playlistContent);

        IEnumerable<M3uEntry> entries = document.Entries.Where(e => !string.IsNullOrEmpty(e.Url));
        if (context.LiveOnly)
        {
            entries = entries.Where(e => LiveClassifier.IsLive(e.Url));
        }

        HashSet<string>? keepGroups = null;
        if (!string.IsNullOrEmpty(context.GroupsFile))
        {
            keepGroups = GroupSelectionFile.LoadKeepSet(context.GroupsFile);
            if (_diagnostics != TextWriter.Null)
            {
                await _diagnostics.WriteLineAsync($"Loaded {keepGroups.Count} kept groups from {context.GroupsFile}.");
            }
        }

        if (keepGroups is not null && keepGroups.Count > 0)
        {
            entries = entries.Where(e => e.Group is not null && keepGroups.Contains(e.Group));
        }
        else if (keepGroups is not null)
        {
            entries = Enumerable.Empty<M3uEntry>();
        }

        var selected = entries.ToList();
        if (selected.Count == 0)
        {
            await _stderr.WriteLineAsync("Warning: no channels matched the provided filters.");
        }

        await WritePlaylistAsync(document, selected, playlistOut, cancellationToken);

        if (epgRequested)
        {
            var epgContent = await fetcher.GetStringAsync(context.EpgSource!, cancellationToken);
            await WriteEpgAsync(epgContent, epgOut, cancellationToken);
        }

        await _stdout.WriteLineAsync($"Kept {selected.Count} channel(s).");
        return ExitCodes.Success;
    }

    private async Task WritePlaylistAsync(PlaylistDocument document, IReadOnlyList<M3uEntry> entries, string? outputPath, CancellationToken cancellationToken)
    {
        var lines = document.EnumerateLines(entries).ToList();
        if (string.IsNullOrEmpty(outputPath))
        {
            foreach (var line in lines)
            {
                await _stdout.WriteLineAsync(line);
            }
        }
        else if (outputPath == "-")
        {
            foreach (var line in lines)
            {
                await _stdout.WriteLineAsync(line);
            }
        }
        else
        {
            await TextFileWriter.WriteAtomicAsync(outputPath, lines, cancellationToken);
            await _stdout.WriteLineAsync($"Playlist written to {outputPath}");
        }
    }

    private async Task WriteEpgAsync(string content, string? outputPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(outputPath) || outputPath == "-")
        {
            await _stdout.WriteAsync(content);
            if (!content.EndsWith('\n'))
            {
                await _stdout.WriteLineAsync();
            }
        }
        else
        {
            await TextFileWriter.WriteAtomicTextAsync(outputPath, content, cancellationToken);
            await _stdout.WriteLineAsync($"EPG written to {outputPath}");
        }
    }
}

using Spectre.Console;
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
    private readonly IAnsiConsole _console;

    public GroupsCommand(
        TextWriter stdout,
        TextWriter stderr,
        TextWriter diagnostics,
        HttpClient httpClient,
        PlaylistParser parser,
        IAnsiConsole? console = null)
    {
        _stdout = stdout;
        _stderr = stderr;
        _diagnostics = diagnostics;
        _httpClient = httpClient;
        _parser = parser;
        _console = console ?? Spectre.Console.AnsiConsole.Console;
    }

    public async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(context.PlaylistSource))
        {
            throw new CliException("Missing required: --playlist-url or --config with playlist", ExitCodes.ConfigError);
        }

        var fetcher = new SourceFetcher(_httpClient, _diagnostics);
        
        var playlistContent = await fetcher.GetStringWithProgressAsync(context.PlaylistSource, _console, cancellationToken);

        PlaylistDocument document = null!;
        await _console.Status()
            .StartAsync("Parsing playlist...", async ctx =>
            {
                document = await Task.Run(() => _parser.Parse(playlistContent), cancellationToken);
            });

        IEnumerable<M3uEntry> entries = document.Entries.Where(e => !string.IsNullOrEmpty(e.Url));
        if (context.LiveOnly)
        {
            entries = entries.Where(e => LiveClassifier.IsLive(e.Url));
        }

        var groups = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        await _console.Status()
            .StartAsync("Extracting groups...", async ctx =>
            {
                await Task.Run(() =>
                {
                    foreach (var entry in entries)
                    {
                        if (!string.IsNullOrEmpty(entry.Group))
                        {
                            groups.Add(entry.Group);
                        }
                    }
                }, cancellationToken);
            });

        if (_diagnostics != TextWriter.Null)
        {
            await _diagnostics.WriteLineAsync($"Discovered {groups.Count} groups.");
        }

        var outputLines = TemplateHeader.Concat(groups).ToList();
        var outPath = context.OutputPath;
        if (string.IsNullOrEmpty(outPath) || outPath == "-")
        {
            _console.MarkupLine($"[blue]Displaying {groups.Count} discovered groups:[/]");
            foreach (var line in outputLines)
            {
                _console.WriteLine(line);
            }
        }
        else
        {
            await TextFileWriter.WriteAtomicAsync(outPath, outputLines, cancellationToken);
            _console.MarkupLine($"[green]? {groups.Count} groups written to {outPath}[/]");
        }

        return ExitCodes.Success;
    }
}

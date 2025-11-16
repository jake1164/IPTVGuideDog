using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using IPTVGuideDog.Core.IO;
using IPTVGuideDog.Core.M3u;
using IPTVGuideDog.Core.Net;
using IPTVGuideDog.Core;

namespace IPTVGuideDog.Cli.Commands;

public sealed class GroupsCommand
{
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

    [RequiresUnreferencedCode("Command execution may use configuration loading with YAML which requires reflection.")]
    [RequiresDynamicCode("Command execution may use configuration loading with YAML which may require dynamic code generation.")]
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
                document = await Task.Run(() => _parser.Parse(playlistContent, cancellationToken), cancellationToken);
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

        var outPath = context.GroupsOutputPath;
        var force = context.Options.IsFlagSet("force");
        if (string.IsNullOrEmpty(outPath) || outPath == "-")
        {
            // Write to stdout
            var header = GroupsFileValidator.CreateHeader();
            var outputLines = header.Concat(groups).ToList();
            _console.MarkupLine($"[blue]Displaying {groups.Count} discovered groups:[/]");
            foreach (var line in outputLines)
            {
                _console.WriteLine(line);
            }
        }
        else
        {
            // Check if file exists and merge with existing groups
            if (File.Exists(outPath))
            {
                // Validate the existing file (unless --force is used)
                if (!force)
                {
                    var validation = await GroupsFileValidator.ValidateFileAsync(outPath, cancellationToken);
                    
                    if (!validation.IsValid)
                    {
                        await _stderr.WriteLineAsync($"Warning: {validation.ErrorMessage}");
                        await _stderr.WriteLineAsync("The file will NOT be modified.");
                        await _stderr.WriteLineAsync("Use --force to override this check.");
                        
                        return ExitCodes.ConfigError;
                    }

                    if (validation.FileVersion != null && _diagnostics != TextWriter.Null)
                    {
                        await _diagnostics.WriteLineAsync($"Existing file version: {validation.FileVersion}");
                    }
                }
                else
                {
                    // Force is enabled, validate to show warning but proceed anyway
                    var validation = await GroupsFileValidator.ValidateFileAsync(outPath, cancellationToken);
                    if (!validation.IsValid)
                    {
                        await _stderr.WriteLineAsync($"Warning: {validation.ErrorMessage}");
                        await _stderr.WriteLineAsync("Proceeding due to --force flag.");
                    }
                }

                var result = await MergeWithExistingGroupsFileAsync(outPath, groups, cancellationToken);
                
                if (result.NewGroups.Count > 0)
                {
                    // Only create backup if we're actually making changes
                    var backup = GroupsFileValidator.CreateBackupPath(outPath);
                    await GroupsFileValidator.CreateBackupAsync(outPath, cancellationToken);
                    
                    await TextFileWriter.WriteAtomicAsync(outPath, result.OutputLines, cancellationToken);
                    
                    _console.MarkupLine($"[green]Added {result.NewGroups.Count} new group(s) to {outPath}[/]");
                    _console.MarkupLine($"[dim]Backup saved to: {backup}[/]");
                    _console.MarkupLine("[yellow]New groups found:[/]");
                    foreach (var newGroup in result.NewGroups)
                    {
                        _console.MarkupLine($"  [cyan]{newGroup}[/]");
                    }
                }
                else
                {
                    _console.MarkupLine($"[green]No new groups found. File {outPath} unchanged.[/]");
                }
            }
            else
            {
                // File doesn't exist, create new one
                var header = GroupsFileValidator.CreateHeader();
                var outputLines = header.Concat(groups).ToList();
                await TextFileWriter.WriteAtomicAsync(outPath, outputLines, cancellationToken);
                _console.MarkupLine($"[green]{groups.Count} groups written to {outPath}[/]");
            }
        }

        return ExitCodes.Success;
    }

    private async Task<MergeResult> MergeWithExistingGroupsFileAsync(
        string filePath, 
        SortedSet<string> discoveredGroups, 
        CancellationToken cancellationToken)
    {
        var existingLines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        var existingGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var outputLines = new List<string>();
        var headerProcessed = false;
        var hasVersionLine = false;

        // Process existing file
        foreach (var line in existingLines)
        {
            // Check if this is the version line
            if (line.TrimStart().StartsWith("######  Created with iptv version ", StringComparison.Ordinal))
            {
                // Update version line to current version with proper padding
                var currentVersion = GroupsFileValidator.GetCurrentVersion();
                var versionLine = $"######  Created with iptv version {currentVersion}";
                var paddedVersionLine = versionLine.PadRight(82) + " ######";
                outputLines.Add(paddedVersionLine);
                hasVersionLine = true;
                headerProcessed = true;
                continue;
            }

            outputLines.Add(line);
            
            // Skip header lines and empty lines
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("######"))
            {
                headerProcessed = true;
                continue;
            }

            if (headerProcessed)
            {
                // Extract group name (with or without # or ## prefix)
                var trimmed = line.TrimStart();
                var groupName = trimmed.TrimStart('#').Trim();
                if (!string.IsNullOrWhiteSpace(groupName))
                {
                    existingGroups.Add(groupName);
                }
            }
        }

        // If no version line was found, add one after the header
        if (!hasVersionLine)
        {
            var currentVersion = GroupsFileValidator.GetCurrentVersion();
            var versionLine = $"######  Created with iptv version {currentVersion}";
            var paddedVersionLine = versionLine.PadRight(82) + " ######";
            // Insert version line after the header lines
            var insertIndex = 0;
            for (int i = 0; i < outputLines.Count; i++)
            {
                if (outputLines[i].TrimStart().StartsWith("######"))
                {
                    insertIndex = i + 1;
                }
                else if (!string.IsNullOrWhiteSpace(outputLines[i]))
                {
                    break;
                }
            }
            outputLines.Insert(insertIndex, paddedVersionLine);
        }

        // Find new groups
        var newGroups = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in discoveredGroups)
        {
            if (!existingGroups.Contains(group))
            {
                newGroups.Add(group);
            }
        }

        // Add new groups with ## prefix to mark them as new
        foreach (var newGroup in newGroups)
        {
            outputLines.Add($"##{newGroup}");
        }

        return new MergeResult(outputLines, newGroups);
    }

    private sealed record MergeResult(List<string> OutputLines, SortedSet<string> NewGroups);
}

using Iptv.Cli.Configuration;

namespace Iptv.Cli.Commands;

public sealed class CommandContext
{
    public CommandKind Kind { get; }
    public CommandOptionSet Options { get; }
    public ProfileConfig? Profile { get; }
    public string? ConfigPath { get; }
    public string? ConfigDirectory { get; }
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; }
    public string? PlaylistSource { get; }
    public string? EpgSource { get; }
    public string? GroupsFile { get; }
    public string? OutputPath { get; }
    public string? PlaylistOutputPath { get; }
    public string? EpgOutputPath { get; }
    public bool LiveOnly { get; }
    public bool Verbose { get; }

    public CommandContext(
        CommandKind kind,
        CommandOptionSet options,
        ProfileConfig? profile,
        string? configPath,
        string? configDirectory,
        IReadOnlyDictionary<string, string> environmentVariables,
        string? playlistSource,
        string? epgSource,
        string? groupsFile,
        string? outputPath,
        string? playlistOutputPath,
        string? epgOutputPath,
        bool liveOnly,
        bool verbose)
    {
        Kind = kind;
        Options = options;
        Profile = profile;
        ConfigPath = configPath;
        ConfigDirectory = configDirectory;
        EnvironmentVariables = environmentVariables;
        PlaylistSource = playlistSource;
        EpgSource = epgSource;
        GroupsFile = groupsFile;
        OutputPath = outputPath;
        PlaylistOutputPath = playlistOutputPath;
        EpgOutputPath = epgOutputPath;
        LiveOnly = liveOnly;
        Verbose = verbose;
    }
}

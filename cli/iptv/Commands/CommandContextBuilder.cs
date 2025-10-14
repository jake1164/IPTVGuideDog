using Iptv.Cli.Configuration;
using Iptv.Cli.Env;

namespace Iptv.Cli.Commands;

public static class CommandContextBuilder
{
    private static readonly HashSet<string> GroupsAllowedOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "playlist-url", "config", "profile", "out-groups", "verbose", "live", "force"
    };
    private static readonly HashSet<string> RunAllowedOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "playlist-url", "epg-url", "groups-file", "out-playlist", "out-epg", "config", "profile", "verbose", "live"
    };

    public static async Task<CommandContext> CreateAsync(
        CommandOptionSet options,
        CommandKind kind,
        TextWriter diagnostics,
        CancellationToken cancellationToken)
    {
        // Validate options
        var allowed = kind == CommandKind.Groups ? GroupsAllowedOptions : RunAllowedOptions;
        foreach (var opt in options.Keys)
        {
            if (!allowed.Contains(opt))
            {
                throw new CommandOptionException($"Unknown or unsupported option '--{opt}' for command '{kind.ToString().ToLowerInvariant()}'");
            }
        }

        string? configPath = options.GetSingleValue("config");
        ProfileConfig? profile = null;
        string? configDir = null;

        if (!string.IsNullOrEmpty(configPath))
        {
            var profileName = options.GetSingleValue("profile") ?? "default";
            var result = await ConfigLoader.LoadProfileAsync(configPath, profileName, cancellationToken);
            profile = result.Profile;
            configDir = result.ConfigDirectory;
            if (diagnostics != TextWriter.Null)
            {
                await diagnostics.WriteLineAsync($"Loaded profile '{profileName}' from {configPath}");
            }
        }

        // Use shell's working directory for zero-config mode
        var envDirectory = configDir ?? Directory.GetCurrentDirectory();
        var env = EnvFileLoader.LoadFromDirectory(envDirectory);
        var foundKeys = env.Keys.Where(k => k.Equals("USER", StringComparison.OrdinalIgnoreCase) || k.Equals("PASS", StringComparison.OrdinalIgnoreCase)).ToList();
        List<string> playlistReplaced = new();
        List<string> epgReplaced = new();
        var playlistSource = UrlSubstitutor.SubstituteCredentials(
            options.GetSingleValue("playlist-url") ?? profile?.Inputs?.Playlist?.Url,
            env,
            out playlistReplaced);

        var epgSource = UrlSubstitutor.SubstituteCredentials(
            options.GetSingleValue("epg-url") ?? profile?.Inputs?.Epg?.Url,
            env,
            out epgReplaced);

        var groupsFile = options.GetSingleValue("groups-file")
            ?? profile?.Filters?.GroupsFile
            ?? profile?.Filters?.DropListFile;

        var groupsOutputPath = options.GetSingleValue("out-groups");
        var playlistOutput = options.GetSingleValue("out-playlist") ?? profile?.Output?.PlaylistPath;
        var epgOutput = options.GetSingleValue("out-epg") ?? profile?.Output?.EpgPath;

        var liveOnly = options.IsFlagSet("live");
        var verbose = options.IsFlagSet("verbose");

        if (verbose && diagnostics != TextWriter.Null)
        {
            if (env.Count > 0)
            {
                await diagnostics.WriteLineAsync($"[VERBOSE] .env file found: {Path.Combine(envDirectory, ".env")}");
                await diagnostics.WriteLineAsync($"[VERBOSE] Keys found: {string.Join(", ", foundKeys)}");
                if (playlistReplaced.Count > 0)
                    await diagnostics.WriteLineAsync($"[VERBOSE] Playlist URL: replaced {string.Join(", ", playlistReplaced)}");
                if (epgReplaced.Count > 0)
                    await diagnostics.WriteLineAsync($"[VERBOSE] EPG URL: replaced {string.Join(", ", epgReplaced)}");
            }
            else
            {
                await diagnostics.WriteLineAsync($"[VERBOSE] No .env file found or no USER/PASS keys present.");
            }
        }

        return new CommandContext(
            kind,
            options,
            profile,
            configPath,
            configDir,
            env,
            playlistSource,
            epgSource,
            groupsFile,
            groupsOutputPath,
            playlistOutput,
            epgOutput,
            liveOnly,
            verbose);
    }
}

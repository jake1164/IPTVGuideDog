namespace Iptv.Cli.Configuration;

public sealed class IptvConfig
{
    public Dictionary<string, ProfileConfig> Profiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ProfileConfig
{
    public InputsConfig? Inputs { get; init; }
    public FiltersConfig? Filters { get; init; }
    public OutputConfig? Output { get; init; }
}

public sealed class InputsConfig
{
    public EndpointConfig? Playlist { get; init; }
    public EndpointConfig? Epg { get; init; }
}

public sealed class EndpointConfig
{
    public string? Url { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public int? TimeoutSeconds { get; init; }
    public int? Retries { get; init; }
    public int? MaxDownloadMb { get; init; }
}

public sealed class FiltersConfig
{
    public List<string>? IncludeGroups { get; init; }
    public List<string>? ExcludeGroups { get; init; }
    public string? DropListFile { get; init; }
    public string? GroupsFile { get; init; }
}

public sealed class OutputConfig
{
    public string? PlaylistPath { get; init; }
    public string? EpgPath { get; init; }
    public bool? AtomicWrites { get; init; }
    public bool? Gzip { get; init; }
    public string? TmpDir { get; init; }
}

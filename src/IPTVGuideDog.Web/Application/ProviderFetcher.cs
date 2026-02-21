using System.Text.RegularExpressions;
using IPTVGuideDog.Core.M3u;
using IPTVGuideDog.Web.Data.Entities;

namespace IPTVGuideDog.Web.Application;

/// <summary>
/// Singleton service that fetches and parses provider playlists and XMLTV guides.
/// Stateless — safe to use from background services.
/// </summary>
public sealed class ProviderFetcher(
    IHttpClientFactory httpClientFactory,
    PlaylistParser playlistParser,
    EnvironmentVariableService envVarService)
{
    private static readonly Regex MetadataAttributeRegex =
        new("(?<key>[A-Za-z0-9\\-]+)=\"(?<value>[^\"]*)\"", RegexOptions.Compiled);

    private static readonly string EmptyXmltvDocument =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?><tv generator-info-name=\"IPTVGuideDog\"></tv>";

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public async Task<PlaylistFetchResult> FetchPlaylistAsync(Provider provider, CancellationToken cancellationToken)
    {
        string content;
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(provider.TimeoutSeconds));

            using var client = httpClientFactory.CreateClient();
            ApplyHeadersFromJson(client, provider.HeadersJson);
            if (!string.IsNullOrWhiteSpace(provider.UserAgent))
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(provider.UserAgent);
            }

            var playlistUrl = SubstituteProviderUrl(provider.PlaylistUrl);
            content = await client.GetStringAsync(playlistUrl, timeoutCts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ProviderFetchException($"Playlist fetch failed: {ex.Message}", ex);
        }

        List<ParsedProviderChannel> channels;
        try
        {
            var document = playlistParser.Parse(content, cancellationToken);
            channels = document.Entries
                .Where(x => !string.IsNullOrWhiteSpace(x.Url))
                .Select(ParseEntry)
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ProviderParseException($"Playlist parse failed: {ex.Message}", ex);
        }

        return new PlaylistFetchResult(
            Channels: channels,
            Bytes: System.Text.Encoding.UTF8.GetByteCount(content));
    }

    public async Task<XmltvFetchResult> FetchXmltvAsync(Provider provider, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(provider.XmltvUrl))
        {
            return new XmltvFetchResult(Xml: EmptyXmltvDocument, Bytes: 0);
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(provider.TimeoutSeconds));

            using var client = httpClientFactory.CreateClient();
            ApplyHeadersFromJson(client, provider.HeadersJson);
            if (!string.IsNullOrWhiteSpace(provider.UserAgent))
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(provider.UserAgent);
            }

            var xmltvUrl = SubstituteProviderUrl(provider.XmltvUrl);
            var xml = await client.GetStringAsync(xmltvUrl, timeoutCts.Token);
            return new XmltvFetchResult(Xml: xml, Bytes: System.Text.Encoding.UTF8.GetByteCount(xml));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // XMLTV failure is non-fatal — caller logs and falls back to empty guide
            throw new ProviderFetchException($"XMLTV fetch failed: {ex.Message}", ex);
        }
    }

    // -------------------------------------------------------------------------
    // Internal helpers (also used by ProviderApiEndpoints via internal access)
    // -------------------------------------------------------------------------

    internal static ParsedProviderChannel ParseEntry(M3uEntry entry)
    {
        var metadata = entry.MetadataLines.FirstOrDefault() ?? string.Empty;
        var attributes = MetadataAttributeRegex.Matches(metadata)
            .Select(match => (Key: match.Groups["key"].Value, Value: match.Groups["value"].Value))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Value, StringComparer.OrdinalIgnoreCase);

        attributes.TryGetValue("tvg-id", out var tvgId);
        attributes.TryGetValue("tvg-name", out var tvgName);
        attributes.TryGetValue("tvg-logo", out var logoUrl);
        attributes.TryGetValue("group-title", out var groupTitleAttr);

        var groupTitle = !string.IsNullOrWhiteSpace(entry.Group)
            ? entry.Group!.Trim()
            : string.IsNullOrWhiteSpace(groupTitleAttr) ? null : groupTitleAttr.Trim();

        var providerChannelKey = NormalizeProviderChannelKey(tvgId);
        var displayName = string.IsNullOrWhiteSpace(entry.Title)
            ? (string.IsNullOrWhiteSpace(tvgName) ? "Unnamed Channel" : tvgName.Trim())
            : entry.Title.Trim();

        return new ParsedProviderChannel
        {
            ProviderChannelKey = providerChannelKey,
            DisplayName = displayName,
            TvgId = string.IsNullOrWhiteSpace(tvgId) ? null : tvgId.Trim(),
            TvgName = string.IsNullOrWhiteSpace(tvgName) ? null : tvgName.Trim(),
            LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim(),
            StreamUrl = entry.Url!.Trim(),
            GroupTitle = groupTitle,
        };
    }

    internal static void ApplyHeadersFromJson(HttpClient client, string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson))
        {
            return;
        }

        using var document = System.Text.Json.JsonDocument.Parse(headersJson);
        if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != System.Text.Json.JsonValueKind.String)
            {
                continue;
            }

            var value = property.Value.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            client.DefaultRequestHeaders.Remove(property.Name);
            client.DefaultRequestHeaders.TryAddWithoutValidation(property.Name, value);
        }
    }

    private string SubstituteProviderUrl(string url)
    {
        try
        {
            return envVarService.SubstituteEnvVars(url);
        }
        catch (InvalidOperationException ex)
        {
            throw new ProviderFetchException(
                $"Provider URL contains undefined environment variables: {ex.Message}", ex);
        }
    }

    internal static string? NormalizeProviderChannelKey(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

// -------------------------------------------------------------------------
// Result types
// -------------------------------------------------------------------------

public sealed record PlaylistFetchResult(
    IReadOnlyList<ParsedProviderChannel> Channels,
    long Bytes);

public sealed record XmltvFetchResult(
    string Xml,
    long Bytes);

// -------------------------------------------------------------------------
// Channel record (replaces private ParsedChannel in ProviderApiEndpoints)
// -------------------------------------------------------------------------

public sealed class ParsedProviderChannel
{
    public string? ProviderChannelKey { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? TvgId { get; init; }
    public string? TvgName { get; init; }
    public string? LogoUrl { get; init; }
    public string StreamUrl { get; init; } = string.Empty;
    public string? GroupTitle { get; init; }
}

// -------------------------------------------------------------------------
// Exceptions
// -------------------------------------------------------------------------

public sealed class ProviderFetchException(string message, Exception? inner = null)
    : Exception(message, inner);

public sealed class ProviderParseException(string message, Exception? inner = null)
    : Exception(message, inner);

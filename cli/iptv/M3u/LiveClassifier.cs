namespace Iptv.Cli.M3u;

public static class LiveClassifier
{
    private static readonly string[] LiveSegments = ["live", "lives"];

    public static bool IsLive(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url.Contains("/live/", StringComparison.OrdinalIgnoreCase);
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => LiveSegments.Contains(segment, StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (QueryContainsLive(uri.Query))
        {
            return true;
        }

        return url.Contains("/live/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool QueryContainsLive(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return false;
        }

        var pairs = query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(kv[0]);
            if (!string.Equals(key, "type", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(key, "kind", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
            if (string.Equals(value, "live", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

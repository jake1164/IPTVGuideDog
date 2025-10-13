namespace Iptv.Cli.Env;

public static class UrlSubstitutor
{
    public static string? SubstituteCredentials(string? value, IReadOnlyDictionary<string, string> env)
    {
        if (string.IsNullOrEmpty(value) || env.Count == 0)
        {
            return value;
        }

        string result = value;
        foreach (var kvp in env)
        {
            if (!string.Equals(kvp.Key, "USER", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(kvp.Key, "PASS", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var upper = kvp.Key.ToUpperInvariant();
            result = result.Replace($"${upper}", kvp.Value, StringComparison.OrdinalIgnoreCase);
            result = result.Replace($"${{{upper}}}", kvp.Value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}

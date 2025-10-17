namespace Iptv.Cli.Env;

public static class UrlSubstitutor
{
    public static string? SubstituteCredentials(string? value, IReadOnlyDictionary<string, string> env, out List<string> replaced)
    {
        replaced = new List<string>();
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
            var lower = kvp.Key.ToLowerInvariant();
            var before = result;
            
            // Replace %USER%, %user%, %PASS%, %pass% (case-insensitive)
            result = result.Replace($"%{upper}%", kvp.Value, StringComparison.Ordinal);
            result = result.Replace($"%{lower}%", kvp.Value, StringComparison.Ordinal);
            
            // Track if replaced
            if (!string.Equals(before, result, StringComparison.Ordinal))
            {
                replaced.Add(upper);
            }
        }

        return result;
    }
}

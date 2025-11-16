namespace IPTVGuideDog.Core.Env;

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
            var before = result;
            
            // Replace %KEY% with value (case-insensitive key matching)
            var pattern = $"%{kvp.Key}%";
            result = result.Replace(pattern, kvp.Value, StringComparison.OrdinalIgnoreCase);
            
            // Track if replaced
            if (!string.Equals(before, result, StringComparison.Ordinal))
            {
                replaced.Add(kvp.Key.ToUpperInvariant());
            }
        }

        return result;
    }
}

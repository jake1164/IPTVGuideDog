namespace Iptv.Cli.IO;

public static class GroupSelectionFile
{
    public static HashSet<string> LoadKeepSet(string path)
    {
        try
        {
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                if (line.StartsWith('#'))
                {
                    var groupName = line.TrimStart('#').Trim();
                    if (!string.IsNullOrEmpty(groupName))
                    {
                        keep.Add(groupName);
                    }
                }
            }
            return keep;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CliException($"Failed to read groups file '{path}': {ex.Message}", ExitCodes.IoError);
        }
    }
}

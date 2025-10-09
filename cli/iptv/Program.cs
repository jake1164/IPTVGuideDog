using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace iptv;

internal class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 2; // config/validation error
        }

        var cmd = args[0].ToLower();
        var options = ParseOptions(args);

        switch (cmd)
        {
            case "groups":
                return RunGroups(options);
            case "run":
                return RunPipeline(options);
            default:
                Console.Error.WriteLine($"Unknown command: {cmd}");
                PrintUsage();
                return 2;
        }
    }

    static Dictionary<string, string> ParseOptions(string[] args)
    {
        var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                var key = args[i].Substring(2);
                var value = (i + 1 < args.Length && !args[i + 1].StartsWith("--")) ? args[++i] : "true";
                opts[key] = value;
            }
        }
        return opts;
    }

    static int RunGroups(Dictionary<string, string> opts)
    {
        // Example: validate required options
        if (!opts.ContainsKey("playlist-url") && !opts.ContainsKey("config"))
        {
            Console.Error.WriteLine("Missing required: --playlist-url or --config");
            return 2;
        }
        Console.WriteLine("Running 'groups' command...");
        // TODO: Implement actual logic
        return 0;
    }

    static int RunPipeline(Dictionary<string, string> opts)
    {
        if (!opts.ContainsKey("playlist-url") && !opts.ContainsKey("config"))
        {
            Console.Error.WriteLine("Missing required: --playlist-url or --config");
            return 2;
        }
        Console.WriteLine("Running 'run' command...");
        // TODO: Implement actual logic
        return 0;
    }

    static void PrintUsage()
    {
        Console.WriteLine(@"
Usage:
  iptv groups   [options]
  iptv run      [options]

Commands:
  groups    Create or refresh the group selection file.
  run       One-shot pipeline: fetch → filter → write.

Options (see cli_spec.md for full details):
  --playlist-url <url>
  --config <path>
  --profile <name>
  --out <path>
  --groups-file <path>
  --out-playlist <path>
  --out-epg <path>
  --epg-url <url>
  --verbose
");
    }
}
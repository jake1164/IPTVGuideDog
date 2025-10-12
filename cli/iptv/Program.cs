using System;
using Microsoft.Extensions.Configuration;

namespace iptv;

internal class Program
{
    static int Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddCommandLine(args)
            .Build();

        var cmd = args.Length > 0 ? args[0].ToLower() : null;

        if (string.IsNullOrEmpty(cmd) || (cmd != "groups" && cmd != "run"))
        {
            PrintUsage();
            return 2;
        }

        // Remove the command from the args for option parsing
        var optionArgs = args.Length > 1 ? args[1..] : Array.Empty<string>();
        var options = new ConfigurationBuilder()
            .AddCommandLine(optionArgs)
            .Build();

        switch (cmd)
        {
            case "groups":
                return RunGroups(options);
            case "run":
                return RunPipeline(options);
            default:
                PrintUsage();
                return 2;
        }
    }

    static int RunGroups(IConfiguration options)
    {
        var playlistUrl = options["playlist-url"];
        var configPath = options["config"];
        var outPath = options["out"]; // <-- Add this line
        var live = options["live"] != null;

        if (playlistUrl == null && configPath == null)
        {
            Console.Error.WriteLine("Missing required: --playlist-url or --config");
            return 2;
        }
        Console.WriteLine("Running 'groups' command...");
        if (live)
        {
            Console.WriteLine("  --live flag detected: Only live streams will be enumerated.");
        }
        if (outPath == null)
        {
            Console.WriteLine("Groups will be written to stdout.");
        }
        else
        {
            Console.WriteLine($"Groups will be written to: {outPath}");
        }
        // TODO: Implement actual logic
        return 0;
    }

    static int RunPipeline(IConfiguration options)
    {
        var playlistUrl = options["playlist-url"];
        var epgUrl = options["epg-url"];
        var outPlaylist = options["out-playlist"];
        var outEpg = options["out-epg"];

        // Validate required inputs
        if (playlistUrl == null && options["config"] == null)
        {
            Console.Error.WriteLine("Missing required: --playlist-url or --config");
            return 2;
        }

        // Validate output requirements
        if (epgUrl != null && outEpg == null)
        {
            Console.Error.WriteLine("Missing required: --out-epg when --epg-url is provided");
            return 2;
        }
        if (epgUrl != null && outPlaylist == null && outEpg == null)
        {
            Console.Error.WriteLine("At least one of --out-playlist or --out-epg must be provided when both outputs are needed.");
            return 2;
        }

        Console.WriteLine("Running 'run' command...");
        // Output logic
        if (outPlaylist == null)
            Console.WriteLine("Playlist will be written to stdout.");
        if (outEpg == null && epgUrl != null)
            Console.WriteLine("EPG will be written to stdout.");

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
  --live           (optional; only live streams are processed)
");
    }
}
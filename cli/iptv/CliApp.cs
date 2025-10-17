using Iptv.Cli.Commands;
using Iptv.Cli.Configuration;
using Iptv.Cli.Env;
using Iptv.Cli.IO;
using Iptv.Cli.M3u;
using Iptv.Cli.Net;

namespace Iptv.Cli;

public sealed class CliApp
{
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;
    private readonly HttpClient _httpClient;

    public CliApp(TextWriter stdout, TextWriter stderr)
    {
        _stdout = stdout;
        _stderr = stderr;
        _httpClient = HttpClientFactory.CreateDefault();
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        if (args.Length == 0)
        {
            UsagePrinter.PrintUsage(_stdout);
            return ExitCodes.ConfigError;
        }

        var commandName = args[0].ToLowerInvariant();
        var optionArgs = args.Skip(1).ToArray();

        CommandOptionSet options;
        try
        {
            options = CommandOptionParser.Parse(optionArgs);
        }
        catch (CommandOptionException ex)
        {
            await _stderr.WriteLineAsync($"Error: {ex.Message}");
            UsagePrinter.PrintUsage(_stdout);
            return ExitCodes.ConfigError;
        }

        var diagnostics = options.IsFlagSet("verbose") ? _stdout : TextWriter.Null;

        try
        {
            switch (commandName)
            {
                case "groups":
                {
                    var context = await CommandContextBuilder.CreateAsync(options, CommandKind.Groups, diagnostics, cancellationToken);
                    var command = new GroupsCommand(_stdout, _stderr, diagnostics, _httpClient, new PlaylistParser());
                    return await command.ExecuteAsync(context, cancellationToken);
                }
                case "run":
                {
                    var context = await CommandContextBuilder.CreateAsync(options, CommandKind.Run, diagnostics, cancellationToken);
                    var command = new RunCommand(_stdout, _stderr, diagnostics, _httpClient, new PlaylistParser());
                    return await command.ExecuteAsync(context, cancellationToken);
                }
                default:
                    UsagePrinter.PrintUsage(_stdout);
                    return ExitCodes.ConfigError;
            }
        }
        catch (CommandOptionException ex)
        {
            await _stderr.WriteLineAsync($"Error: {ex.Message}");
            UsagePrinter.PrintUsage(_stdout);
            return ExitCodes.ConfigError;
        }
        catch (CliException ex)
        {
            await _stderr.WriteLineAsync(ex.Message);
            return ex.ExitCode;
        }
    }
}

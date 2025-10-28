namespace IPTVGuideDog.Cli;

public sealed class CliException : Exception
{
    public int ExitCode { get; }

    public CliException(string message, int exitCode)
        : base(message)
    {
        ExitCode = exitCode;
    }
}

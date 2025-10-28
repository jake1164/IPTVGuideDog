namespace IPTVGuideDog.Cli.Commands;

public sealed class CommandOptionException : Exception
{
    public CommandOptionException(string message) : base(message)
    {
    }
}

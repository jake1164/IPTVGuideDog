using System.Threading.Tasks;
using IPTVGuideDog.Cli;

try
{
    using (var app = new CliApp(Console.Out, Console.Error))
        return await app.RunAsync(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Fatal error: {ex.Message}");
    return 1;
}

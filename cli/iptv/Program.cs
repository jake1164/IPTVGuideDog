using System.Threading.Tasks;
using Iptv.Cli;

var app = new CliApp(Console.Out, Console.Error);
return await app.RunAsync(args);

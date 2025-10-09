using System;
using Microsoft.Extensions.Configuration;

namespace iptv;

internal class Program
{
    static void Main(string[] args)
    {
        var cmdline = new ConfigurationBuilder()
            .AddCommandLine(args)
            .Build();

        Console.WriteLine("Hello World!");
    }
}
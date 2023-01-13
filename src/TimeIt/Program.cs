// See https://aka.ms/new-console-template for more information

using CliWrap;
using CliWrap.Buffered;

Console.WriteLine("Hello, World!");

for (var i = 0; i < 10; i++)
{
    var result = await Cli.Wrap("ls")
        .ExecuteBufferedAsync()
        .ConfigureAwait(false);

    // Console.WriteLine(result.StandardOutput);
    Console.WriteLine(result.RunTime.TotalMilliseconds);
}

var config = TimeIt.Configuration.Config.LoadConfiguration(args[0]);

Console.WriteLine(config);
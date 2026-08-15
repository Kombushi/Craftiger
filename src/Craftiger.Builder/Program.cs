using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Craftiger.Builder.Services;
using Microsoft.Extensions.DependencyInjection;

// TODO: replace Console[.Error].WriteLine logging with ILogger

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: Craftiger.Builder <dump.sqlite> [--output <dir>] [--pack-version <version>]");
    return 1;
}

var dumpPath = args[0];
var outputDir = ".";
var packVersion = "2.9.0-beta-2";
var imagesPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!, "image.zip");
string? explainItem = null;
for (var i = 1; i < args.Length - 1; i++)
{
    if (args[i] == "--output")
    {
        outputDir = args[i + 1];
    }
    if (args[i] == "--pack-version")
    {
        packVersion = args[i + 1];
    }
    if (args[i] == "--images")
    {
        imagesPath = args[i + 1];
    }
    if (args[i] == "--explain")
    {
        explainItem = args[i + 1];
    }
}

if (!File.Exists(dumpPath))
{
    Console.Error.WriteLine($"Dump not found: {dumpPath}");
    return 1;
}
Directory.CreateDirectory(outputDir);

using var services = new ServiceCollection()
    .AddBuilderServices()
    .BuildServiceProvider();

return services.
    GetRequiredService<IBuilderPipeline>()
    .Run(new BuilderOptions(dumpPath, outputDir, packVersion, imagesPath, explainItem));

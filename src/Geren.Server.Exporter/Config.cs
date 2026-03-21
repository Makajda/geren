using Microsoft.Extensions.Configuration;

namespace Geren.Server.Exporter;

internal sealed record Settings(
    string Project = "",
    string OutputDirectory = "",
    string OutputFileName = "",
    string Configuration = "Release",
    string Platform = "AnyCPU");

internal static class Config {
    internal static Settings? Get(string[] args) {
#if DEBUG
        args = ["--project", @"C:\depo\source\Qer\Src\Qera\Qera.csproj", "--output-dir", @"Aut"];//todo validate dir?
#endif

        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args, SwitchMappings)
            .Build();

        var settings = config.Get<Settings>() ?? new();

        if (string.IsNullOrEmpty(settings.Project) || string.IsNullOrEmpty(settings.OutputDirectory)) {
            PrintUsage();
            return null;
        }

        if (string.IsNullOrEmpty(settings.OutputFileName))
            settings = settings with { OutputFileName = $"{Path.GetFileNameWithoutExtension(settings.Project)}.minimalapi.json" };

        return settings;
    }

    internal static Dictionary<string, string> SwitchMappings = new() {
            { "-p", "Project" },
            { "--project", "Project" },

            { "-o", "OutputDirectory" },
            { "--output-dir", "OutputDirectory" },

            { "-f", "OutputFileName" },
            { "--output-file", "OutputFileName" },

            { "-c", "Configuration" },
            { "--configuration", "Configuration" },

            { "--platform", "Platform" },
        };

    private static void PrintUsage() {
        Console.WriteLine("Geren.Server.Exporter");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  Geren.Server.Exporter --project <path.csproj> --output-dir <folder> [options]");
        Console.WriteLine("  Geren.Server.Exporter -p <path.csproj> -o <folder> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -f, --output-file <name>     Output file name (default: <ProjectName>.minimalapi.json)");
        Console.WriteLine("  -c, --configuration <cfg>    MSBuild Configuration (default: Release)");
        Console.WriteLine("      --platform <platform>    MSBuild Platform (default: AnyCPU)");
        Console.WriteLine("  -h, --help                   Show help");
    }
}


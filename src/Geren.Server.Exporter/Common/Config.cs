using Microsoft.Extensions.Configuration;

namespace Geren.Server.Exporter.Common;

internal sealed record Settings(
    string Project = "",
    string OutputDirectory = "",
    string OutputFileName = "",
    string[]? ExcludeTypes = null,
    string Configuration = "Release",
    string Platform = "AnyCPU");

internal static class Config {
    internal const int ExitOk = 0;
    internal const int ExitUsage = 2;

    internal static bool TryGet(string[] args, out Settings settings, out int exitCode) {
        settings = new();
        exitCode = ExitUsage;

        if (args.Any(static a => IsHelpArg(a))) {
            PrintUsage();
            exitCode = ExitOk;
            return false;
        }

        int spi = args.IndexOf("--settings-path", StringComparer.OrdinalIgnoreCase);
        if (spi < 0) spi = args.IndexOf("-s", StringComparer.OrdinalIgnoreCase);
        string settingsPath = spi >= 0 && spi < args.Length - 1 ? args[spi + 1] : "none";

        IConfigurationBuilder configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(settingsPath, optional: true)
            .AddCommandLine(args, SwitchMappings);

        if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development")
            configBuilder.AddUserSecrets<Spinner>();// Project & OutputDirectory

        IConfiguration configuration = configBuilder.Build();
        settings = configuration.Get<Settings>() ?? new();

        if (string.IsNullOrEmpty(settings.Project) || string.IsNullOrEmpty(settings.OutputDirectory)) {
            PrintUsage();
            exitCode = ExitUsage;
            return false;
        }

        if (string.IsNullOrEmpty(settings.OutputFileName))
            settings = settings with { OutputFileName = $"{Path.GetFileNameWithoutExtension(settings.Project)}.gerenapi.json" };

        exitCode = ExitOk;
        return true;
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
        Console.WriteLine("  geren-server-exporter --project <path.csproj> --output-dir <folder> [options]");
        Console.WriteLine("  geren-server-exporter -p <path.csproj> -o <folder> [options]");
        Console.WriteLine("  geren-server-exporter -project <path.csproj> -o <folder> [options]");
        Console.WriteLine("  geren-server-exporter -s <settings.json> [options]");
        Console.WriteLine("  geren-server-exporter --settings-path <settings.json> [options]");
        Console.WriteLine("  geren-server-exporter -h | --help");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  MapGroup prefixes are detected only for compile-time constant strings.");
        Console.WriteLine("  Avoid MapGroup(Func<string>)/reflection-based wrappers for route prefixes.");
        Console.WriteLine();
        Console.WriteLine("Important:");
        Console.WriteLine("  Add the types you want to exclude from parameters to the settings.json file.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -f, --output-file <name>       Output file name (default: <ProjectName>.gerenapi.json)");
        Console.WriteLine("  -c, --configuration <cfg>      MSBuild Configuration (default: Release)");
        Console.WriteLine("      --platform <platform>      MSBuild Platform (default: AnyCPU)");
        Console.WriteLine("  -h, --help                     Show help");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0   Success (or --help)");
        Console.WriteLine("  1   Unexpected error");
        Console.WriteLine("  2   Invalid arguments / usage");
        Console.WriteLine("  3   Failed to create Compilation");
        Console.WriteLine("  4   Canceled");
    }

    private static bool IsHelpArg(string arg)
        => string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase)
           || string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
           || string.Equals(arg, "/?", StringComparison.OrdinalIgnoreCase);
}

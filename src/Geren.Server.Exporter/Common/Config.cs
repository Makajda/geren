using Microsoft.Extensions.Configuration;

namespace Geren.Server.Exporter.Common;

internal sealed record Settings(
    string Project = "",
    string OutputDirectory = "",
    string OutputFileName = "",
    string[]? Include = null,
    string[]? Exclude = null,
    string[]? ExcludeTypes = null,
    string Configuration = "Release",
    string Platform = "AnyCPU");

internal static class Config {
    internal static bool TryGet(string[] args, out Settings settings, out int exitCode) {
        settings = new();
        exitCode = Given.ExitUsage;

        if (args.Any(static a => IsHelpArg(a))) {
            PrintUsage();
            exitCode = Given.ExitOk;
            return false;
        }

        // CommandLineConfigurationProvider does not reliably bind repeated switches into string[].
        // Parse include/exclude explicitly (supports multiple occurrences and ';' separated values).
        var includeFromArgs = ReadMulti(args, "--include", "-i");
        var excludeFromArgs = ReadMulti(args, "--exclude", "-x");
        var argsForConfig = RemoveMulti(RemoveMulti(args, "--include", "-i"), "--exclude", "-x");

        int spi = args.IndexOf("--settings-path", StringComparer.OrdinalIgnoreCase);
        if (spi < 0) spi = args.IndexOf("-s", StringComparer.OrdinalIgnoreCase);
        string settingsPath = spi >= 0 && spi < args.Length - 1 ? args[spi + 1] : "none";

        IConfigurationBuilder configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(settingsPath, optional: true)
            .AddCommandLine(argsForConfig, SwitchMappings);

        if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development")
            configBuilder.AddUserSecrets<Spinner>();// Project & OutputDirectory

        IConfiguration configuration = configBuilder.Build();
        settings = configuration.Get<Settings>() ?? new();

        if (string.IsNullOrEmpty(settings.Project) || string.IsNullOrEmpty(settings.OutputDirectory)) {
            PrintUsage();
            exitCode = Given.ExitUsage;
            return false;
        }

        if (includeFromArgs.Length != 0)
            settings = settings with { Include = Merge(settings.Include, includeFromArgs) };

        if (excludeFromArgs.Length != 0)
            settings = settings with { Exclude = Merge(settings.Exclude, excludeFromArgs) };

        if (string.IsNullOrEmpty(settings.OutputFileName))
            settings = settings with { OutputFileName = $"{Path.GetFileNameWithoutExtension(settings.Project)}.gerenapi.json" };

        exitCode = Given.ExitOk;
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
        Console.WriteLine("  -i, --include <rule>           Include endpoints matching rule (repeatable).");
        Console.WriteLine("  -x, --exclude <rule>           Exclude endpoints matching rule (repeatable).");
        Console.WriteLine("                                Rule examples:");
        Console.WriteLine("                                  path:/stat/*");
        Console.WriteLine("                                  route:re:^/v[0-9]+/");
        Console.WriteLine("                                  namespace:MyCompany.MyApi.*");
        Console.WriteLine("                                Notes:");
        Console.WriteLine("                                  - Rules are OR-ed within include/exclude lists.");
        Console.WriteLine("                                  - If any include rules exist, only matching endpoints are kept.");
        Console.WriteLine("                                  - Exclude rules always win.");
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

    private static string[] ReadMulti(string[] args, string longSwitch, string shortSwitch) {
        var items = new List<string>();
        for (var i = 0; i < args.Length; i++) {
            var a = args[i];
            if (a is null)
                continue;

            if (a.StartsWith(longSwitch + "=", StringComparison.OrdinalIgnoreCase)) {
                Add(items, a[(longSwitch.Length + 1)..]);
                continue;
            }

            if (a.StartsWith(shortSwitch + "=", StringComparison.OrdinalIgnoreCase)) {
                Add(items, a[(shortSwitch.Length + 1)..]);
                continue;
            }

            if (string.Equals(a, longSwitch, StringComparison.OrdinalIgnoreCase) || string.Equals(a, shortSwitch, StringComparison.OrdinalIgnoreCase)) {
                if (i + 1 < args.Length)
                    Add(items, args[i + 1]);

                continue;
            }
        }

        return items.ToArray();
    }

    private static void Add(List<string> items, string? raw) {
        if (string.IsNullOrWhiteSpace(raw))
            return;

        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            if (!string.IsNullOrWhiteSpace(part))
                items.Add(part);
        }
    }

    private static string[] Merge(string[]? existing, string[] extra) {
        if (existing is null || existing.Length == 0)
            return extra;

        if (extra.Length == 0)
            return existing;

        var merged = new string[existing.Length + extra.Length];
        Array.Copy(existing, merged, existing.Length);
        Array.Copy(extra, 0, merged, existing.Length, extra.Length);
        return merged;
    }

    private static string[] RemoveMulti(string[] args, string longSwitch, string shortSwitch) {
        if (args.Length == 0)
            return args;

        List<string> kept = new(args.Length);
        for (var i = 0; i < args.Length; i++) {
            string a = args[i];
            if (a is null) {
                continue;
            }

            if (a.StartsWith(longSwitch + "=", StringComparison.OrdinalIgnoreCase)
                || a.StartsWith(shortSwitch + "=", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            if (string.Equals(a, longSwitch, StringComparison.OrdinalIgnoreCase)
                || string.Equals(a, shortSwitch, StringComparison.OrdinalIgnoreCase)) {
                if (i + 1 < args.Length)
                    i++; // skip value
                continue;
            }

            kept.Add(a);
        }

        return [.. kept];
    }
}

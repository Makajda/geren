namespace Geren.Server.Exporter;

internal sealed record CliOptions(string ProjectPath, string OutputDirectory, string OutputFileName, string Configuration, string Platform) {
    public static CliOptions? Parse(string[] args) {
        CliOptions? options = default;
        string? error = default;

        string? project = null;
        string? outDir = null;
        string? outFile = null;
        string configuration = "Release";
        string platform = "AnyCPU";

        for (var i = 0; i < args.Length; i++) {
            var a = args[i];
            switch (a) {
                case "--project":
                case "-p":
                    if (!TryReadValue(args, ref i, out project, out error)) return options;
                    break;
                case "--output-dir":
                case "-o":
                    if (!TryReadValue(args, ref i, out outDir, out error)) return options;
                    break;
                case "--output-file":
                case "-f":
                    if (!TryReadValue(args, ref i, out outFile, out error)) return options;
                    break;
                case "--configuration":
                case "-c":
                    if (!TryReadValue(args, ref i, out configuration, out error)) return options;
                    break;
                case "--platform":
                    if (!TryReadValue(args, ref i, out platform, out error)) return options;
                    break;
                case "--help":
                case "-h":
                case "/?":
                    error = null;
                    return options;
                default:
                    error = $"Unknown argument: {a}";
                    return options;
            }
        }

        if (string.IsNullOrWhiteSpace(project)) {
            error = "Missing required argument: --project <path-to-csproj>";
            return options;
        }

        if (string.IsNullOrWhiteSpace(outDir)) {
            error = "Missing required argument: --output-dir <folder>";
            return options;
        }

        var projectPath = Path.GetFullPath(project);
        if (!File.Exists(projectPath)) {
            error = $"Project file does not exist: {projectPath}";
            return options;
        }

        var outputDirectory = Path.GetFullPath(outDir);
        var outputFileName = string.IsNullOrWhiteSpace(outFile)
            ? $"{Path.GetFileNameWithoutExtension(projectPath)}.minimalapi.json"
            : outFile;

        options = new(
            ProjectPath: projectPath,
            OutputDirectory: outputDirectory,
            OutputFileName: outputFileName,
            Configuration: configuration,
            Platform: platform);
        return options;
    }

    private static bool TryReadValue(string[] args, ref int i, out string value, out string? error) {
        if (i + 1 >= args.Length) {
            value = "";
            error = $"Missing value for '{args[i]}'";
            return false;
        }

        i++;
        value = args[i];
        error = null;
        return true;
    }

    public static void PrintUsage() {
        Console.WriteLine("Geren.Server.Exporter");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  Geren.Server.Exporter --project <path.csproj> --output-dir <folder> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -f, --output-file <name>     Output file name (default: <ProjectName>.minimalapi.json)");
        Console.WriteLine("  -c, --configuration <cfg>    MSBuild Configuration (default: Release)");
        Console.WriteLine("      --platform <platform>    MSBuild Platform (default: AnyCPU)");
        Console.WriteLine("  -h, --help                   Show help");
    }
}

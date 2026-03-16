using Geren.SpecExporter;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using System.Text;

namespace Geren.Server.Exporter;

internal static class Program {
    public static async Task<int> Main(string[] args) {
        if (!CliOptions.TryParse(args, out var options, out var error)) {
            if (error is not null) {
                Console.Error.WriteLine(error);
                Console.Error.WriteLine();
            }
            CliOptions.PrintUsage();
            return 2;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => {
            e.Cancel = true;
            cts.Cancel();
        };

        try {
            if (!MSBuildLocator.IsRegistered) {
                MSBuildLocator.RegisterDefaults();
            }

            var workspaceProperties = new Dictionary<string, string>(StringComparer.Ordinal) {
                ["Configuration"] = options.Configuration,
                ["Platform"] = options.Platform,
            };

            using var workspace = MSBuildWorkspace.Create(workspaceProperties);
            workspace.RegisterWorkspaceFailedHandler(e => {
                if (e.Diagnostic.Kind == Microsoft.CodeAnalysis.WorkspaceDiagnosticKind.Failure) {
                    Console.Error.WriteLine(e.Diagnostic.Message);
                }
            });

            var project = await workspace.OpenProjectAsync(options.ProjectPath, cancellationToken: cts.Token).ConfigureAwait(false);
            var compilation = await project.GetCompilationAsync(cts.Token).ConfigureAwait(false);
            if (compilation is null) {
                Console.Error.WriteLine("Failed to create Compilation.");
                return 1;
            }

            var warnings = new List<string>();
            var endpoints = Extractor.Extract(compilation, warnings, cts.Token);

            foreach (var w in warnings) {
                Console.Error.WriteLine(w);
            }

            var json = JsonWriter.Write(endpoints);

            Directory.CreateDirectory(options.OutputDirectory);
            var outputPath = Path.Combine(options.OutputDirectory, options.OutputFileName);
            await File.WriteAllTextAsync(
                    outputPath,
                    json,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    cts.Token)
                .ConfigureAwait(false);

            Console.WriteLine($"Wrote {endpoints.Count} endpoints to '{outputPath}'.");
            return 0;
        }
        catch (OperationCanceledException) {
            Console.Error.WriteLine("Canceled.");
            return 130;
        }
        catch (Exception ex) {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    private sealed record CliOptions(string ProjectPath, string OutputDirectory, string OutputFileName, string Configuration, string Platform) {
        public static bool TryParse(string[] args, out CliOptions options, out string? error) {
            options = default!;
            error = null;

            string? project = null;
            string? outDir = null;
            string? outFile = null;
            var configuration = "Release";
            var platform = "AnyCPU";

            for (var i = 0; i < args.Length; i++) {
                var a = args[i];
                switch (a) {
                    case "--project":
                    case "-p":
                        if (!TryReadValue(args, ref i, out project, out error)) return false;
                        break;
                    case "--output-dir":
                    case "-o":
                        if (!TryReadValue(args, ref i, out outDir, out error)) return false;
                        break;
                    case "--output-file":
                    case "-f":
                        if (!TryReadValue(args, ref i, out outFile, out error)) return false;
                        break;
                    case "--configuration":
                    case "-c":
                        if (!TryReadValue(args, ref i, out configuration, out error)) return false;
                        break;
                    case "--platform":
                        if (!TryReadValue(args, ref i, out platform, out error)) return false;
                        break;
                    case "--help":
                    case "-h":
                    case "/?":
                        error = null;
                        return false;
                    default:
                        error = $"Unknown argument: {a}";
                        return false;
                }
            }

            if (string.IsNullOrWhiteSpace(project)) {
                error = "Missing required argument: --project <path-to-csproj>";
                return false;
            }

            if (string.IsNullOrWhiteSpace(outDir)) {
                error = "Missing required argument: --output-dir <folder>";
                return false;
            }

            var projectPath = Path.GetFullPath(project);
            if (!File.Exists(projectPath)) {
                error = $"Project file does not exist: {projectPath}";
                return false;
            }

            var outputDirectory = Path.GetFullPath(outDir);
            var outputFileName = string.IsNullOrWhiteSpace(outFile)
                ? $"{Path.GetFileNameWithoutExtension(projectPath)}.minimalapi.json"
                : outFile;

            options = new CliOptions(
                ProjectPath: projectPath,
                OutputDirectory: outputDirectory,
                OutputFileName: outputFileName,
                Configuration: configuration,
                Platform: platform);
            return true;
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
            Console.WriteLine("Geren.SpecExporter");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  Geren.SpecExporter --project <path.csproj> --output-dir <folder> [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -f, --output-file <name>     Output file name (default: <ProjectName>.minimalapi.json)");
            Console.WriteLine("  -c, --configuration <cfg>    MSBuild Configuration (default: Release)");
            Console.WriteLine("      --platform <platform>    MSBuild Platform (default: AnyCPU)");
            Console.WriteLine("  -h, --help                   Show help");
        }
    }
}


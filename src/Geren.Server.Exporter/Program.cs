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

        using CancellationTokenSource cts = new();
        Console.CancelKeyPress += (_, e) => {
            e.Cancel = true;
            cts.Cancel();
        };

        try {
            if (!MSBuildLocator.IsRegistered)
                MSBuildLocator.RegisterDefaults();

            var workspaceProperties = new Dictionary<string, string>(StringComparer.Ordinal) {
                ["Configuration"] = options.Configuration,
                ["Platform"] = options.Platform,
            };

            using var workspace = MSBuildWorkspace.Create(workspaceProperties);
            workspace.RegisterWorkspaceFailedHandler(e => {
                if (e.Diagnostic.Kind == Microsoft.CodeAnalysis.WorkspaceDiagnosticKind.Failure)
                    Console.Error.WriteLine(e.Diagnostic.Message);
            });

            var project = await workspace.OpenProjectAsync(options.ProjectPath, cancellationToken: cts.Token).ConfigureAwait(false);
            var compilation = await project.GetCompilationAsync(cts.Token).ConfigureAwait(false);
            if (compilation is null) {
                Console.Error.WriteLine("Failed to create Compilation.");
                return 1;
            }

            List<Endpoint> endpoints = [];
            List<string> warnings = [];
            Extractor.Extract(compilation, endpoints, warnings, cts.Token);

            foreach (var w in warnings)
                Console.Error.WriteLine(w);

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

}


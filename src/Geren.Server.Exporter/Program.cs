using Geren.Server.Exporter.Extract;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using System.Text.Json;

namespace Geren.Server.Exporter;

internal static class Program {
    public static async Task<int> Main(string[] args) {
        if (!Config.TryGet(args, out Settings settings, out int exitCode))
            return exitCode;

        if (!EndpointFilters.TryCreate(settings.Include, settings.Exclude, out var filters, out var filterError)) {
            Console.Error.WriteLine("Invalid include/exclude filter rule:");
            Console.Error.WriteLine(filterError);
            Console.Error.WriteLine();
            Console.Error.WriteLine("Tip: use --help for filter rule examples.");
            return Given.ExitUsage;
        }

        using CancellationTokenSource cts = new();
        Console.CancelKeyPress += (_, e) => {
            e.Cancel = true;
            cts.Cancel();
        };

        try {
            using Spinner spinner1 = new("Creating Compilation", ConsoleColor.Green);
            Compilation? compilation = await CreateCompilation(settings, cts.Token);
            spinner1.Dispose();
            if (compilation is null) {
                Console.Error.WriteLine("Failed to create Compilation.");
                return Given.ExitCompilation;
            }

            using Spinner spinner2 = new("Extracting", ConsoleColor.Blue);
            var (endpoints, warnings) = Extractor.Extract(compilation, settings.ExcludeTypes ?? [], filters, cts.Token);
            spinner2.Dispose();

            string log = Dide.ToString(warnings);
            Console.Error.WriteLine(log);

            Directory.CreateDirectory(settings.OutputDirectory);

            var warningPath = Path.Combine(settings.OutputDirectory, Path.GetFileNameWithoutExtension(settings.OutputFileName) + ".log");
            await Save(warningPath, log, cts.Token).ConfigureAwait(false);

            ErDocument document = new("1.0.0", [.. endpoints]);
            string json = JsonSerializer.Serialize(document, Givens.JsonSerializerOptions);

            var outputPath = Path.Combine(settings.OutputDirectory, settings.OutputFileName);
            await Save(outputPath, json, cts.Token).ConfigureAwait(false);

            Console.WriteLine($"Wrote {endpoints.Length} endpoints to");
            Console.WriteLine(outputPath);
            return Given.ExitOk;
        }
        catch (OperationCanceledException) {
            Console.Error.WriteLine("Canceled.");
            return Given.ExitCanceled;
        }
        catch (Exception ex) {
            Console.Error.WriteLine(ex.ToString());
            return Given.ExitUnexpected;
        }
    }

    private static async Task<Compilation?> CreateCompilation(Settings settings, CancellationToken token) {
        if (!MSBuildLocator.IsRegistered)
            MSBuildLocator.RegisterDefaults();

        var workspaceProperties = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["Configuration"] = settings.Configuration,
            ["Platform"] = settings.Platform,
        };

        using MSBuildWorkspace workspace = MSBuildWorkspace.Create(workspaceProperties);
        workspace.RegisterWorkspaceFailedHandler(e => {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure) {
                Console.Error.WriteLine();
                Console.Error.WriteLine(e.Diagnostic.Message);
            }
        });

        Project project = await workspace.OpenProjectAsync(settings.Project, cancellationToken: token).ConfigureAwait(false);
        Compilation? compilation = await project.GetCompilationAsync(token).ConfigureAwait(false);
        return compilation;
    }

    private static async Task Save(string filePath, string text, CancellationToken cancellationToken) {
        await File.WriteAllTextAsync(
                filePath,
                text,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken)
            .ConfigureAwait(false);
    }
}

using Geren.Server.Exporter.Extract;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geren.Server.Exporter;

internal static class Program {
    public static async Task<int> Main(string[] args) {
        Settings? settings = Config.Get(args);
        if (settings is null)
            return 2;

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
                return 3;
            }

            using Spinner spinner2 = new("Extracting", ConsoleColor.Blue);
            var (endpoints, warnings) = Extractor.Extract(compilation, settings.ExcludeTypes ?? [], cts.Token);
            spinner2.Dispose();

            var logs = Dide.ToStrings(warnings);
            foreach (string l in logs)
                Console.Error.WriteLine(l);

            Directory.CreateDirectory(settings.OutputDirectory);

            var warningPath = Path.Combine(settings.OutputDirectory, Path.GetFileNameWithoutExtension(settings.OutputFileName) + ".log");
            await Save(warningPath, string.Join('\n', logs), cts.Token).ConfigureAwait(false);

            ErDocument document = new("1.0.0", endpoints);
            string json = JsonSerializer.Serialize(document, options);

            var outputPath = Path.Combine(settings.OutputDirectory, settings.OutputFileName);
            await Save(outputPath, json, cts.Token).ConfigureAwait(false);

            Console.WriteLine($"Wrote {endpoints.Length} endpoints to");
            Console.WriteLine(outputPath);
            return 0;
        }
        catch (OperationCanceledException) {
            Console.Error.WriteLine("Canceled.");
            return 4;
        }
        catch (Exception ex) {
            Console.Error.WriteLine(ex.ToString());
            return 1;
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

    private static readonly JsonSerializerOptions options = new() {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static async Task Save(string filePath, string text, CancellationToken cancellationToken) {
        await File.WriteAllTextAsync(
                filePath,
                text,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken)
            .ConfigureAwait(false);
    }
}

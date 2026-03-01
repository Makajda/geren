using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Geren.Tests;

public sealed class GenerationSnapshotTests {
    private const string DefaultSource = "public sealed class Marker { }";

    [Fact]
    public void Snapshot_SimpleGet_Matches_Expected_Generated_Code() => AssertSnapshotCase("SimpleGet");

    [Fact]
    public void Snapshot_DeleteWithJsonBody_Matches_Expected_Generated_Code() => AssertSnapshotCase("DeleteJsonBody");

    [Fact]
    public void Snapshot_CustomRootNamespace_Matches_Expected_Generated_Code() => AssertSnapshotCase("CustomRootNamespace");

    private static void AssertSnapshotCase(string caseName) {
        var caseDirectory = Path.Combine(AppContext.BaseDirectory, "Snapshots", caseName);
        Directory.Exists(caseDirectory).Should().BeTrue($"Snapshot case directory must exist: {caseDirectory}");

        var openApiPath = Path.Combine(caseDirectory, "input.openapi.json");
        File.Exists(openApiPath).Should().BeTrue($"OpenAPI input must exist for case '{caseName}'");

        var sourcePath = Path.Combine(caseDirectory, "source.cs");
        var source = File.Exists(sourcePath)
            ? File.ReadAllText(sourcePath)
            : DefaultSource;

        var rootNamespacePath = Path.Combine(caseDirectory, "rootNamespace.txt");
        var rootNamespace = File.Exists(rootNamespacePath)
            ? File.ReadAllText(rootNamespacePath).Trim()
            : null;

        var result = GeneratorTestHarness.RunGenerator(
            source,
            File.ReadAllText(openApiPath),
            openApiPath: "v1.json",
            rootNamespace: rootNamespace);

        result.Diagnostics.Should().NotContain(
            static d => d.Severity == DiagnosticSeverity.Error,
            $"snapshot case '{caseName}' should generate without errors");

        var expectedFiles = Directory.GetFiles(caseDirectory, "*.g.cs", SearchOption.TopDirectoryOnly);
        expectedFiles.Should().NotBeEmpty($"Snapshot case '{caseName}' must contain at least one expected .g.cs file");

        var duplicateNames = result.GeneratedSources
            .GroupBy(static s => GeneratorTestHarness.ToSnapshotFileName(s.HintName), StringComparer.Ordinal)
            .Where(static g => g.Count() > 1)
            .Select(static g => g.Key)
            .ToArray();
        duplicateNames.Should().BeEmpty("Generated hint names in snapshot test should map to unique snapshot file names");

        var actualByName = result.GeneratedSources.ToDictionary(
            static s => GeneratorTestHarness.ToSnapshotFileName(s.HintName),
            static s => GeneratorTestHarness.NormalizeCode(s.SourceText.ToString()),
            StringComparer.Ordinal);

        var globalFiles = new[] { "FactoryBridge.g.cs", "Extensions.g.cs" };
        var actualSet = actualByName.Keys
            .Where(key => !globalFiles.Contains(key, StringComparer.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        var expectedSet = expectedFiles
            .Select(Path.GetFileName)
            .Where(fileName => !globalFiles.Contains(fileName, StringComparer.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        actualSet.Should().BeEquivalentTo(expectedSet, $"generated files for case '{caseName}' should match the snapshot set");

        foreach (var expectedFile in expectedFiles) {
            var fileName = Path.GetFileName(expectedFile);
            actualByName.Should().ContainKey(fileName, $"generated sources should contain '{fileName}' for case '{caseName}'");

            var expectedText = GeneratorTestHarness.NormalizeCode(File.ReadAllText(expectedFile));
            var actualText = actualByName[fileName];
            actualText.Should().Be(expectedText, $"{caseName}/{fileName} snapshot mismatch");
        }
    }
}

namespace Geren.Client.Generator.Tests.Integration;

public sealed class GeneratorEndToEndTests {
    [Fact]
    public void Generator_SkipsFilesWithoutOptInMetadata() {
        var compilation = CompilationFactory.Create("t", "public sealed class Dummy { }");

        var run = GeneratorRunner.Run(
            compilation,
            additionalFiles: [
                new GeneratorRunner.AdditionalFile(
                    Path: @"C:\specs\petstore.json",
                    Text: MinimalGetSpec("/pets"),
                    OptIn: false)
            ],
            rootNamespace: "Acme");

        run.DriverResult.Diagnostics.Should().BeEmpty();
        run.DriverResult.Results.Should().ContainSingle();
        run.DriverResult.Results[0].GeneratedSources.Should().BeEmpty();
    }

    [Fact]
    public void Generator_OptedInFile_EmitsFactoryBridgeExtensionsAndClient() {
        var compilation = CompilationFactory.Create("t", "public sealed class Dummy { }");

        var run = GeneratorRunner.Run(
            compilation,
            additionalFiles: [
                new GeneratorRunner.AdditionalFile(
                    Path: @"C:\specs\petstore.json",
                    Text: MinimalGetSpec("/pets"),
                    OptIn: true)
            ],
            rootNamespace: "Acme");

        var gen = run.DriverResult.Results.Single().GeneratedSources.ToDictionary(static s => s.HintName, static s => s.SourceText.ToString());

        gen.Keys.Should().Contain("_FactoryBridge.g.cs");
        gen.Keys.Should().Contain("_Extensions.g.cs");
        gen.Keys.Should().Contain("WebApiClient.g.cs");
        gen["WebApiClient.g.cs"].Should().Contain("namespace Acme.Petstore;");

        run.OutputCompilation.GetDiagnostics(CancellationToken.None).Where(static d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
    }

    [Fact]
    public void Generator_MultipleDocs_AddsHintPrefix() {
        var compilation = CompilationFactory.Create("t", "public sealed class Dummy { }");

        var run = GeneratorRunner.Run(
            compilation,
            additionalFiles: [
                new GeneratorRunner.AdditionalFile(@"C:\specs\a.json", MinimalGetSpec("/a"), OptIn: true),
                new GeneratorRunner.AdditionalFile(@"C:\specs\b.json", MinimalGetSpec("/b"), OptIn: true),
            ],
            rootNamespace: "Acme");

        var hintNames = run.DriverResult.Results.Single().GeneratedSources.Select(static s => s.HintName).ToArray();

        hintNames.Should().Contain(static name => name.StartsWith("_h") && name.Contains(".Extensions.g.cs", StringComparison.Ordinal));
        hintNames.Should().NotContain("_Extensions.g.cs", "when more than one document is present, hint prefix avoids collisions");
    }

    [Fact]
    public void Generator_UnresolvedSchema_EmitsPlaceholdersFileAndClientCompiles() {
        var compilation = CompilationFactory.Create("t", "public sealed class Dummy { }");

        var run = GeneratorRunner.Run(
            compilation,
            additionalFiles: [
                new GeneratorRunner.AdditionalFile(@"C:\specs\petstore.json", SpecWithUnresolvedMetadataType(), OptIn: true),
            ],
            rootNamespace: "Acme");

        var generated = run.DriverResult.Results.Single().GeneratedSources.ToDictionary(static s => s.HintName, static s => s.SourceText.ToString());

        generated.Keys.Should().Contain("_UnresolvedTypes.g.cs");
        generated["_UnresolvedTypes.g.cs"].Should().Contain("public sealed partial class __GerenUnresolvedType_");
        generated["WebApiClient.g.cs"].Should().Contain("__GerenUnresolvedType_");

        run.DriverResult.Diagnostics.Should().Contain(d => d.Id == "GEREN007");
        run.OutputCompilation.GetDiagnostics(CancellationToken.None).Where(static d => d.Severity == DiagnosticSeverity.Warning).Should().BeEmpty();
    }

    private static string MinimalGetSpec(string path) => $$"""
        {
          "openapi": "3.0.1",
          "info": { "title": "t", "version": "1.0" },
          "paths": {
            "{{path}}": {
              "get": {
                "responses": { "200": { "description": "ok", "content": { "text/plain": { "schema": { "type": "string" } } } } }
              }
            }
          }
        }
        """;

    private static string SpecWithUnresolvedMetadataType() => """
        {
          "openapi": "3.0.1",
          "info": { "title": "t", "version": "1.0" },
          "paths": {
            "/pets": {
              "get": {
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "x-metadata": "Dto.MissingType"
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;
}


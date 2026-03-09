namespace Geren.Tests;

public sealed class ApiClientGeneratorTests {
    [Fact]
    public void Initialize_should_not_generate_sources() {
        var result = RunGenerator(
            compilation: TestCompilationFactory.Create(),
            additionalTexts: ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText("empty.json", EmptyOpenApiText)));

        result.GeneratedSources.Should().BeEmpty();
    }

    [Fact]
    public void Initialize_should_generate_factory_bridge_client_and_extensions_for_valid_openapi() {
        var compilation = TestCompilationFactory.Create(
            userSources: [
                """
                namespace Contracts;
                public sealed class Pet;
                public sealed class CreatePetRequest;
                """
            ]);

        var result = RunGenerator(
            compilation,
            ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText("pet-store.json", ValidOpenApiText)),
            rootNamespace: "Company.Generated");

        result.Diagnostics.Select(static diagnostic => diagnostic.Id).Should().Equal(["GEREN010"]);
        result.GeneratedSources.Should().HaveCount(3);
        result.GeneratedSources.Select(static source => source.HintName).Should().Contain("Company.Generated.FactoryBridge.g.cs");
        result.GeneratedSources.Should().Contain(source => source.HintName.Contains(".WebApiClient.", StringComparison.Ordinal));
        result.GeneratedSources.Should().Contain(source => source.HintName.Contains(".Extensions.", StringComparison.Ordinal));
        result.GeneratedSources.Should().Contain(source => source.Text.Contains("namespace Company.Generated.Pet_store;"));
        result.GeneratedSources.Should().Contain(source => source.Text.Contains("public sealed partial class WebApiClient"));
        result.GeneratedSources.Should().Contain(source => source.Text.Contains("Task<global::Contracts.Pet> GetPets"));
    }

    [Fact]
    public void Initialize_should_not_generate_when_json_probe_fails() {
        var result = RunGenerator(
            compilation: TestCompilationFactory.Create(),
            additionalTexts: ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText("broken.json", "{ not-json }")));

        result.Diagnostics.Select(static diagnostic => diagnostic.Id).Should().Contain("GEREN001");
        result.GeneratedSources.Should().BeEmpty();
    }

    [Fact]
    public void Initialize_should_emit_resilience_capable_sources_when_symbol_is_available() {
        var compilation = TestCompilationFactory.Create(
            userSources: [
                """
                namespace Contracts;
                public sealed class Pet;
                public sealed class CreatePetRequest;
                """
            ],
            includeResilience: true);

        var result = RunGenerator(
            compilation,
            ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText("pet-store.json", ValidOpenApiText)));

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().Contain(source => source.Text.Contains("AddStandardResilienceHandler"));
        result.GeneratedSources.Should().Contain(source => source.Text.Contains("using Polly;"));
    }

    private static GeneratorRunResultModel RunGenerator(
        CSharpCompilation compilation,
        ImmutableArray<AdditionalText> additionalTexts,
        string? rootNamespace = null) {
        var generator = new ApiClientGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            additionalTexts: additionalTexts,
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview),
            optionsProvider: new TestAnalyzerConfigOptionsProvider(
                rootNamespace is null
                    ? null
                    : new Dictionary<string, string> {
                        ["build_property.Geren_RootNamespace"] = rootNamespace
                    }));

        driver = driver.RunGenerators(compilation);
        var runResult = driver.GetRunResult().Results.Single();

        return new GeneratorRunResultModel(
            runResult.Diagnostics,
            [.. runResult.GeneratedSources.Select(static source => new GeneratedSourceModel(source.HintName, source.SourceText.ToString()))]);
    }

    private sealed record GeneratorRunResultModel(
        ImmutableArray<Diagnostic> Diagnostics,
        ImmutableArray<GeneratedSourceModel> GeneratedSources);

    private sealed record GeneratedSourceModel(string HintName, string Text);


    private const string EmptyOpenApiText = """
    {
      "openapi": "3.0.1",
      "info": { "title": "Empty", "version": "1.0" },
      "components": {
        "schemas": {
          "Pet": { "type": "object" },
          "CreatePetRequest": { "type": "object" }
        }
      }
    }
    """;

    private const string ValidOpenApiText = """
    {
      "openapi": "3.0.1",
      "info": { "title": "Pets", "version": "1.0" },
      "paths": {
        "/pets": {
          "get": {
            "responses": {
              "200": {
                "description": "ok",
                "content": {
                  "application/json": {
                    "schema": { "$ref": "#/components/schemas/Pet" }
                  }
                }
              }
            }
          },
          "post": {
            "requestBody": {
              "required": true,
              "content": {
                "application/json": {
                  "schema": { "$ref": "#/components/schemas/CreatePetRequest" }
                }
              }
            },
            "responses": {
              "201": {
                "description": "ok",
                "content": {
                  "application/json": {
                    "schema": { "$ref": "#/components/schemas/Pet" }
                  }
                }
              }
            }
          }
        }
      },
      "components": {
        "schemas": {
          "Pet": { "type": "object" },
          "CreatePetRequest": { "type": "object" }
        }
      }
    }
    """;
}

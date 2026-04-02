namespace Geren.Client.Generator.Tests.Integration;

public sealed class GeneratorGoldenTests {
    [Fact]
    public void Golden_Petstore_EmitsExpectedSources() {
        var compilation = CompilationFactory.Create("t", "public sealed class Dummy { }");

        var run = GeneratorRunner.Run(
            compilation,
            additionalFiles: [
                new GeneratorRunner.AdditionalFile(
                    Path: @"C:\specs\petstore.json",
                    Text: PetstoreSpec(),
                    OptIn: true)
            ],
            rootNamespace: "Acme");

        var gen = run.DriverResult.Results.Single().GeneratedSources.ToDictionary(static s => s.HintName, static s => s.SourceText.ToString());

        gen.Keys.Should().Contain("_FactoryBridge.g.cs");
        gen.Keys.Should().Contain("_Extensions.g.cs");
        gen.Keys.Should().Contain("RootHttp.g.cs");

        Snapshots.ShouldMatch("petstore/_FactoryBridge.g.cs.snap", gen["_FactoryBridge.g.cs"]);
        Snapshots.ShouldMatch("petstore/_Extensions.g.cs.snap", gen["_Extensions.g.cs"]);
        Snapshots.ShouldMatch("petstore/RootHttp.g.cs.snap", gen["RootHttp.g.cs"]);

        run.OutputCompilation.GetDiagnostics(CancellationToken.None)
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty();
    }

    [Fact]
    public void Golden_UnresolvedSchema_EmitsExpectedPlaceholdersFile() {
        var compilation = CompilationFactory.Create("t", "public sealed class Dummy { }");

        var run = GeneratorRunner.Run(
            compilation,
            additionalFiles: [
                new GeneratorRunner.AdditionalFile(
                    Path: @"C:\specs\petstore.json",
                    Text: SpecWithUnresolvedMetadataType(),
                    OptIn: true)
            ],
            rootNamespace: "Acme");

        var gen = run.DriverResult.Results.Single().GeneratedSources.ToDictionary(static s => s.HintName, static s => s.SourceText.ToString());

        gen.Keys.Should().Contain("_UnresolvedTypes.g.cs");
        Snapshots.ShouldMatch("unresolved/_UnresolvedTypes.g.cs.snap", gen["_UnresolvedTypes.g.cs"]);

        run.DriverResult.Diagnostics.Should().Contain(d => d.Id == "GEREN007");
        run.OutputCompilation.GetDiagnostics(CancellationToken.None).Where(static d => d.Severity == DiagnosticSeverity.Warning).Should().BeEmpty();
    }

    private static string PetstoreSpec() => """
        {
          "openapi": "3.0.1",
          "info": { "title": "t", "version": "1.0" },
          "paths": {
            "/ping": {
              "get": {
                "responses": { "200": { "description": "ok", "content": { "text/plain": { "schema": { "type": "string" } } } } }
              }
            },
            "/pets": {
              "get": {
                "parameters": [
                  { "name": "q", "in": "query", "schema": { "type": "string" }, "required": false }
                ],
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "properties": {
                            "id": { "type": "integer", "format": "int32" },
                            "name": { "type": "string" }
                          }
                        }
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
                      "schema": {
                        "type": "object",
                        "properties": {
                          "name": { "type": "string" }
                        }
                      }
                    }
                  }
                },
                "responses": { "204": { "description": "created" } }
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

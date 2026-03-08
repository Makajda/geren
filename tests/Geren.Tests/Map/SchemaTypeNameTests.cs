namespace Geren.Tests.Map;

public sealed class SchemaTypeNameTests {
    [Fact]
    public void Resolve_should_map_primitives_and_arrays() {
        var resolver = CreateResolver();

        resolver.Resolve(new OpenApiSchema { Type = JsonSchemaType.Number }).Should().Be("double");
        resolver.Resolve(new OpenApiSchema { Format = "int64" }).Should().Be("long");
        resolver.Resolve(new OpenApiSchema {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchema { Type = JsonSchemaType.Boolean }
        }).Should().Be("System.Collections.Generic.IReadOnlyList<bool>");
    }

    [Fact]
    public void Resolve_should_use_x_metadata_when_type_exists() {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var resolver = CreateResolver(diagnostics, """namespace Contracts; public sealed class Pet;""");
        var schema = new OpenApiSchema {
            Extensions = new Dictionary<string, IOpenApiExtension> {
                ["x-metadata"] = new JsonNodeExtension(JsonValue.Create("Contracts.Pet")!)
            }
        };

        var typeName = resolver.Resolve(schema);

        typeName.Should().Be("global::Contracts.Pet");
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_should_report_missing_x_metadata_only_once() {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var resolver = CreateResolver(diagnostics);
        var schema = new OpenApiSchema {
            Extensions = new Dictionary<string, IOpenApiExtension> {
                ["x-metadata"] = new JsonNodeExtension(JsonValue.Create("Contracts.MissingPet")!)
            }
        };

        resolver.Resolve(schema).Should().Be("object");
        resolver.Resolve(schema).Should().Be("object");

        diagnostics.Select(static diagnostic => diagnostic.Id).Should().Equal(["GEREN007"]);
    }

    [Fact]
    public void Resolve_should_keep_valid_x_compile_expression() {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var resolver = CreateResolver(
            diagnostics,
            """namespace Contracts; public sealed class Pet;""");
        var schema = new OpenApiSchema {
            Extensions = new Dictionary<string, IOpenApiExtension> {
                ["x-compile"] = new JsonNodeExtension(JsonValue.Create("global::System.Collections.Generic.List<global::Contracts.Pet>")!)
            }
        };

        resolver.Resolve(schema).Should().Be("global::System.Collections.Generic.List<global::Contracts.Pet>");
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_should_report_invalid_x_compile_only_once() {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var resolver = CreateResolver(diagnostics);
        var schema = new OpenApiSchema {
            Extensions = new Dictionary<string, IOpenApiExtension> {
                ["x-compile"] = new JsonNodeExtension(JsonValue.Create("global::Missing<Type")!)
            }
        };

        resolver.Resolve(schema).Should().Be("object");
        resolver.Resolve(schema).Should().Be("object");

        diagnostics.Select(static diagnostic => diagnostic.Id).Should().Equal(["GEREN007"]);
    }

    [Fact]
    public void Resolve_should_use_unique_schema_reference_match() {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var resolver = CreateResolver(
            diagnostics,
            """namespace Contracts; public sealed class Pet;""");
        var schema = GetReferencedSchema("""
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
              }
            }
          },
          "components": {
            "schemas": {
              "Pet": {
                "type": "object"
              }
            }
          }
        }
        """);

        resolver.Resolve(schema).Should().Be("global::Contracts.Pet");
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_should_report_ambiguous_schema_reference() {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var resolver = CreateResolver(
            diagnostics,
            """
            namespace Contracts.A; public sealed class Pet;
            namespace Contracts.B; public sealed class Pet;
            """);
        var schema = GetReferencedSchema("""
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
              }
            }
          },
          "components": {
            "schemas": {
              "Pet": {
                "type": "object"
              }
            }
          }
        }
        """);

        resolver.Resolve(schema).Should().Be("object");
        diagnostics.Select(static diagnostic => diagnostic.Id).Should().Contain("GEREN014");
    }

    private static SchemaTypeName CreateResolver(
        ImmutableArray<Diagnostic>.Builder? diagnostics = null,
        params string[] sources) {
        diagnostics ??= ImmutableArray.CreateBuilder<Diagnostic>();
        return new SchemaTypeName(TestCompilationFactory.Create(sources), diagnostics);
    }

    private static IOpenApiSchema GetReferencedSchema(string text) {
        var document = OpenApiDocumentFactory.Load(text);
        return document.Paths["/pets"].Operations![HttpMethod.Get].Responses!["200"].Content!["application/json"].Schema!;
    }
}

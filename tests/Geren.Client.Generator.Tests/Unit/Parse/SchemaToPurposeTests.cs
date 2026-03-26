namespace Geren.Client.Generator.Tests.Unit.Parse;

public sealed class SchemaToPurposeTests {
    [Fact]
    public void Convert_NullSchema_DefaultsToString() {
        SchemaToPurpose.Convert(schema: null).Name.Should().Be("object");
    }

    [Fact]
    public void Convert_XMetadata_ReturnsMetadataPurpose() {
        var schema = new OpenApiSchema {
            Extensions = new Dictionary<string, IOpenApiExtension>()
        };
        schema.Extensions!["x-metadata"] = new JsonNodeExtension(JsonValue.Create("Dto.Pet")!);

        var result = SchemaToPurpose.Convert(schema);

        result.By.Should().Be(Byres.Metadata);
        result.Name.Should().Be("Dto.Pet");
    }

    [Fact]
    public void Convert_XCompile_RestoresDisguisedArrays() {
        // Arrays are disguised in ParseInc before OpenApiDocument.Load, then restored here.
        var schema = new OpenApiSchema {
            Extensions = new Dictionary<string, IOpenApiExtension>()
        };
        schema.Extensions["x-compile"] = new JsonNodeExtension(JsonValue.Create("System.Collections.Generic.List<int-->")!);

        var result = SchemaToPurpose.Convert(schema);

        result.By.Should().Be(Byres.Compile);
        result.Name.Should().Be("System.Collections.Generic.List<int[]>");
    }

    [Fact]
    public void Convert_SchemaRef_UsesReferenceId() {
        const string json = """
            {
              "openapi": "3.0.1",
              "info": { "title": "t", "version": "1.0" },
              "paths": {
                "/pets": {
                  "get": {
                    "operationId": "GetPets",
                    "responses": {
                      "200": {
                        "description": "ok",
                        "content": {
                          "application/json": {
                            "schema": { "$ref": "#/components/schemas/PetDto" }
                          }
                        }
                      }
                    }
                  }
                }
              },
              "components": {
                "schemas": {
                  "PetDto": { "type": "object" }
                }
              }
            }
            """;

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var doc = OpenApiDocument.Load(ms).Document!;
        var schema = doc.Paths["/pets"].Operations![HttpMethod.Get]
            .Responses!["200"].Content!["application/json"].Schema;

        var result = SchemaToPurpose.Convert(schema);

        result.By.Should().Be(Byres.Reference);
        result.Name.Should().Be("PetDto");
    }

    [Fact]
    public void Convert_IntFormats_MapToCSharpAliases() {
        SchemaToPurpose.Convert(new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" }).Name.Should().Be("long");
        SchemaToPurpose.Convert(new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" }).Name.Should().Be("int");
    }

    [Fact]
    public void Convert_Primitives_MapToCSharpAliases() {
        SchemaToPurpose.Convert(new OpenApiSchema { Type = JsonSchemaType.Boolean }).Name.Should().Be("bool");
        SchemaToPurpose.Convert(new OpenApiSchema { Type = JsonSchemaType.Number }).Name.Should().Be("double");
        SchemaToPurpose.Convert(new OpenApiSchema { Type = JsonSchemaType.String }).Name.Should().Be("string");
    }

    [Fact]
    public void Convert_Object_MapsToObject() {
        SchemaToPurpose.Convert(new OpenApiSchema { Type = JsonSchemaType.Object }).Name.Should().Be("object");
    }

    [Fact]
    public void Convert_Array_MapsToIReadOnlyListOfItemType() {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.Boolean } };

        SchemaToPurpose.Convert(schema).Name.Should().Be("System.Collections.Generic.IReadOnlyList<bool>");
    }
}

namespace Geren.Tests.Map;

public sealed class OperationReturnTypeTests {
    [Fact]
    public void Resolve_should_prefer_200_json_over_other_2xx_payloads() {
        var operation = new OpenApiOperation {
            Responses = new OpenApiResponses {
                ["202"] = new OpenApiResponse {
                    Description = "accepted",
                    Content = new Dictionary<string, IOpenApiMediaType> {
                        ["text/plain"] = new OpenApiMediaType()
                    }
                },
                ["200"] = new OpenApiResponse {
                    Description = "ok",
                    Content = new Dictionary<string, IOpenApiMediaType> {
                        ["application/json"] = new OpenApiMediaType {
                            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                        }
                    }
                }
            }
        };

        var returnType = OperationReturnType.Resolve(operation, CreateTypeNameResolver());

        returnType.Should().Be("string");
    }

    [Fact]
    public void Resolve_should_map_text_plain_to_string() {
        var operation = new OpenApiOperation {
            Responses = new OpenApiResponses {
                ["201"] = new OpenApiResponse {
                    Description = "created",
                    Content = new Dictionary<string, IOpenApiMediaType> {
                        ["text/plain"] = new OpenApiMediaType()
                    }
                }
            }
        };

        OperationReturnType.Resolve(operation, CreateTypeNameResolver()).Should().Be("string");
    }

    [Fact]
    public void Resolve_should_fall_back_to_default_response_when_2xx_have_no_supported_payload() {
        var operation = new OpenApiOperation {
            Responses = new OpenApiResponses {
                ["204"] = new OpenApiResponse { Description = "empty" },
                ["Default"] = new OpenApiResponse {
                    Description = "fallback",
                    Content = new Dictionary<string, IOpenApiMediaType> {
                        ["application/json"] = new OpenApiMediaType {
                            Schema = new OpenApiSchema { Type = JsonSchemaType.Boolean }
                        }
                    }
                }
            }
        };

        OperationReturnType.Resolve(operation, CreateTypeNameResolver()).Should().Be("bool");
    }

    [Fact]
    public void Resolve_should_return_empty_when_no_supported_payload_exists() {
        var operation = new OpenApiOperation {
            Responses = new OpenApiResponses {
                ["200"] = new OpenApiResponse {
                    Description = "binary",
                    Content = new Dictionary<string, IOpenApiMediaType> {
                        ["application/octet-stream"] = new OpenApiMediaType()
                    }
                }
            }
        };

        OperationReturnType.Resolve(operation, CreateTypeNameResolver()).Should().BeEmpty();
    }

    private static TypeResolver CreateTypeNameResolver() =>
        new("rootFileNamespace", TestCompilationFactory.Create(), ImmutableArray.CreateBuilder<Diagnostic>());
}

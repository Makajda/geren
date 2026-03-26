namespace Geren.Client.Generator.Tests.Unit.Parse;

public sealed class ParseBuildTests {
    [Fact]
    public void Parse_QueryUnsupportedType_ReportsDiagnosticAndOmitsQueryParam() {
        const string json = """
            {
              "openapi": "3.0.1",
              "info": { "title": "t", "version": "1.0" },
              "paths": {
                "/pets": {
                  "get": {
                    "operationId": "GetPets",
                    "parameters": [
                      { "name": "filter", "in": "query", "required": false, "schema": { "type": "object" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        var result = ParseInc.Parse(new InMemoryAdditionalText(@"C:\specs\pets.json", json), "openapi", CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d => d.Id == "GEREN004");
        result.Purpoints.Should().ContainSingle();
        result.Purpoints[0].Queries?.Should().BeEmpty("unsupported query params are omitted from signature");
    }

    [Fact]
    public void Parse_PathPlaceholderMismatch_SkipsEndpointAndReportsDiagnostic() {
        const string json = """
            {
              "openapi": "3.0.1",
              "info": { "title": "t", "version": "1.0" },
              "paths": {
                "/pets/{id}": {
                  "get": {
                    "operationId": "GetPet",
                    "parameters": [
                      { "name": "petId", "in": "path", "required": true, "schema": { "type": "integer", "format": "int32" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        var result = ParseInc.Parse(new InMemoryAdditionalText(@"C:\specs\pets.json", json), "openapi", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle(d => d.Id == "GEREN002");
        result.Purpoints.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MissingParamLocation_ReportsDiagnostic() {
        const string json = """
            {
              "openapi": "3.0.1",
              "info": { "title": "t", "version": "1.0" },
              "paths": {
                "/pets": {
                  "get": {
                    "operationId": "GetPets",
                    "parameters": [
                      { "name": "x", "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        var result = ParseInc.Parse(new InMemoryAdditionalText(@"C:\specs\pets.json", json), "openapi", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle(d => d.Id == "GEREN002");
    }

    [Fact]
    public void Parse_UnsupportedParamLocation_ReportsDiagnostic() {
        const string json = """
            {
              "openapi": "3.0.1",
              "info": { "title": "t", "version": "1.0" },
              "paths": {
                "/pets": {
                  "get": {
                    "operationId": "GetPets",
                    "parameters": [
                      { "name": "x", "in": "header", "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        var result = ParseInc.Parse(new InMemoryAdditionalText(@"C:\specs\pets.json", json), "openapi", CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d => d.Id == "GEREN003");
        result.Purpoints.Should().ContainSingle("unsupported locations are diagnosed but do not prevent generation");
    }

    [Fact]
    public void Parse_QueryOptional_AddsNullableMarker() {
        const string json = """
            {
              "openapi": "3.0.1",
              "info": { "title": "t", "version": "1.0" },
              "paths": {
                "/pets": {
                  "get": {
                    "operationId": "GetPets",
                    "parameters": [
                      { "name": "q", "in": "query", "required": false, "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        var result = ParseInc.Parse(new InMemoryAdditionalText(@"C:\specs\pets.json", json), "openapi", CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Purpoints.Should().ContainSingle();
        result.Purpoints[0].Queries?.Should().ContainSingle(q => q.Name == "q" && q.Type.EndsWith('?'));
    }

    [Fact]
    public void Parse_TextPlainRequestBody_UsesStringBodyType() {
        const string json = """
            {
              "openapi": "3.0.1",
              "info": { "title": "t", "version": "1.0" },
              "paths": {
                "/ping": {
                  "put": {
                    "operationId": "PutPing",
                    "requestBody": {
                      "content": {
                        "text/plain": { "schema": { "type": "string" } }
                      }
                    },
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        var result = ParseInc.Parse(new InMemoryAdditionalText(@"C:\specs\ping.json", json), "openapi", CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Diagnostics.Should().BeEmpty();
        result.Purpoints.Should().ContainSingle();
        result.Purpoints[0].BodyType.Should().Be("string");
        result.Purpoints[0].BodyMedia.Should().Be(MediaTypes.Text_Plain);
    }

    [Fact]
    public void Parse_QueryArrayOfInt_IsSupported() {
        const string json = """
            {
              "openapi": "3.0.1",
              "info": { "title": "t", "version": "1.0" },
              "paths": {
                "/pets": {
                  "get": {
                    "operationId": "GetPets",
                    "parameters": [
                      {
                        "name": "ids",
                        "in": "query",
                        "required": false,
                        "schema": { "type": "array", "items": { "type": "integer", "format": "int32" } }
                      }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        var result = ParseInc.Parse(new InMemoryAdditionalText(@"C:\specs\pets.json", json), "openapi", CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Diagnostics.Should().BeEmpty();
        result.Purpoints.Single().Queries?.Should().ContainSingle(q => q.Name == "ids" && q.Type == "System.Collections.Generic.IReadOnlyList<int>?");
    }

    [Fact]
    public void Parse_KeywordParameterName_IsEscapedInIdentifier() {
        const string json = """
            {
              "openapi": "3.0.1",
              "info": { "title": "t", "version": "1.0" },
              "paths": {
                "/pets": {
                  "get": {
                    "operationId": "GetPets",
                    "parameters": [
                      { "name": "class", "in": "query", "required": false, "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        var result = ParseInc.Parse(new InMemoryAdditionalText(@"C:\specs\pets.json", json), "openapi", CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Purpoints.Single().Queries?.Should().ContainSingle(q => q.Name == "class" && q.Identifier == "class_");
    }

    [Fact]
    public void Parse_PathParameters_AreOverriddenByOperationParameters() {
        const string json = """
            {
              "openapi": "3.0.1",
              "info": { "title": "t", "version": "1.0" },
              "paths": {
                "/pets": {
                  "parameters": [
                    { "name": "q", "in": "query", "required": false, "schema": { "type": "string" } }
                  ],
                  "get": {
                    "operationId": "GetPets",
                    "parameters": [
                      { "name": "q", "in": "query", "required": true, "schema": { "type": "integer", "format": "int32" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        var result = ParseInc.Parse(new InMemoryAdditionalText(@"C:\specs\pets.json", json), "openapi", CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Diagnostics.Should().BeEmpty();
        result.Purpoints.Single().Queries?.Should().ContainSingle(q => q.Name == "q" && q.Type == "int");
    }
}

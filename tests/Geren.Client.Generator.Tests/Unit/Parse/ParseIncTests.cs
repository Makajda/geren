namespace Geren.Client.Generator.Tests.Unit.Parse;

public sealed class ParseIncTests {
    [Fact]
    public void Parse_EmptyFile_ReportsJsonReadError() {
        var file = new InMemoryAdditionalText(path: @"C:\specs\empty.json", text: "   ");

        var result = ParseInc.Parse(file, true, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle(d => d.Id == "GEREN001");
    }

    [Fact]
    public void Parse_InvalidOpenApi_ReportsParseError() {
        var file = new InMemoryAdditionalText(path: @"C:\specs\bad.json", text: "{\"x\":1}");

        var result = ParseInc.Parse(file, true, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle(d => d.Id == "GEREN002");
    }

    [Fact]
    public void Parse_WhenAdditionalTextThrows_ReportsParseError() {
        var file = new ThrowingAdditionalText(@"C:\specs\throws.json");

        var result = ParseInc.Parse(file, true, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle(d => d.Id == "GEREN002");
    }

    [Fact]
    public void Parse_ValidOpenApi_NormalizesPathTemplate() {
        const string json = """
            {
              "openapi": "3.0.1",
              "info": { "title": "t", "version": "1.0" },
              "paths": {
                "/pets/{id}": {
                  "get": {
                    "operationId": "GetPet",
                    "parameters": [
                      { "name": "id", "in": "path", "required": true, "schema": { "type": "integer", "format": "int32" } }
                    ],
                    "responses": {
                      "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "string" } } } }
                    }
                  }
                }
              }
            }
            """;

        var file = new InMemoryAdditionalText(path: @"C:\specs\pets.json", text: json);

        var result = ParseInc.Parse(file, true, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Diagnostics.Should().BeEmpty();
        result.Purpoints.Should().ContainSingle();
        result.Purpoints[0].Path.Should().Be("/pets/{id}");
        result.Purpoints[0].Params?.Should().ContainSingle(p => p.Name == "id" && p.Identifier == "id");
    }

    [Fact]
    public void Parse_UnsupportedRequestBody_SkipsEndpointAndReportsDiagnostic() {
        const string json = """
            {
              "openapi": "3.0.1",
              "info": { "title": "t", "version": "1.0" },
              "paths": {
                "/pets": {
                  "post": {
                    "operationId": "CreatePet",
                    "requestBody": {
                      "content": {
                        "application/xml": { "schema": { "type": "string" } }
                      }
                    },
                    "responses": {
                      "200": { "description": "ok" }
                    }
                  }
                }
              }
            }
            """;

        var file = new InMemoryAdditionalText(path: @"C:\specs\pets.json", text: json);

        var result = ParseInc.Parse(file, true, CancellationToken.None);

        result.Success.Should().BeTrue("the OpenAPI document is valid, only the operation is unsupported");
        result.Diagnostics.Should().ContainSingle(d => d.Id == "GEREN005");
    }

    private sealed class ThrowingAdditionalText(string path) : AdditionalText {
        public override string Path { get; } = path;
        public override SourceText? GetText(CancellationToken cancellationToken = default) => throw new InvalidOperationException("boom");
    }
}

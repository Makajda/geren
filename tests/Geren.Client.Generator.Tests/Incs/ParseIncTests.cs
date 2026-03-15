namespace Geren.Tests.Incs;

public sealed class ParseIncTests {
    [Fact]
    public void Parse_should_return_document_for_valid_openapi() {
        const string json = """
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
                      "application/json": { "schema": { "type": "string" } }
                    }
                  }
                }
              }
            }
          }
        }
        """;

        var result = ParseInc.Parse("pets.json", json);

        result.Document.Should().NotBeNull();
        result.FilePath.Should().Be("pets.json");
        result.Diagnostic.Should().BeNull();
    }

    [Fact]
    public void Parse_should_report_reader_errors() {
        const string json = """{"openapi":true}""";

        var result = ParseInc.Parse("broken.json", json);

        result.Document.Should().BeNull();
        result.FilePath.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.Id.Should().Be("GEREN002");
        result.Diagnostic.GetMessage().Should().Contain("OpenAPI");
    }

    [Fact]
    public void Parse_should_support_x_compile_values_with_array_syntax() {
        const string json = """
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
                        "schema": {
                          "x-compile": "global::System.Collections.Generic.List<global::Contracts.Pet[]>"
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

        var result = ParseInc.Parse("pets.json", json);

        result.Document.Should().NotBeNull();
        result.Diagnostic.Should().BeNull();
    }
}

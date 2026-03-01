using FluentAssertions;
using Xunit;

namespace Geren.Tests;

public sealed class GeneratorDiagnosticsTests {
    [Fact]
    public void Reports_GEREN014_For_Ambiguous_Ref_Type() {
        var source = """
namespace Contracts.V1 { public sealed class User { } }
namespace Contracts.V2 { public sealed class User { } }
public sealed class Marker { }
""";

        var openApi = """
{
  "openapi": "3.0.4",
  "info": { "title": "t", "version": "1" },
  "paths": {
    "/users/{id}": {
      "get": {
        "parameters": [
          { "name": "id", "in": "path", "required": true, "schema": { "type": "integer", "format": "int32" } }
        ],
        "responses": {
          "200": {
            "description": "ok",
            "content": {
              "application/json": { "schema": { "$ref": "#/components/schemas/User" } }
            }
          }
        }
      }
    }
  },
  "components": {
    "schemas": {
      "User": { "type": "object" }
    }
  }
}
""";

        var result = GeneratorTestHarness.RunGenerator(source, openApi);

        result.Diagnostics.Select(static d => d.Id).Should().Contain("GEREN014");
    }

    [Fact]
    public void Reports_GEREN015_For_Path_Placeholder_Parameter_Mismatch() {
        var source = "public sealed class Marker { }";

        var openApi = """
{
  "openapi": "3.0.4",
  "info": { "title": "t", "version": "1" },
  "paths": {
    "/orders/{id:int}": {
      "get": {
        "parameters": [
          { "name": "id:int", "in": "path", "required": true, "schema": { "type": "integer", "format": "int32" } }
        ],
        "responses": {
          "204": { "description": "no content" }
        }
      }
    }
  }
}
""";

        var result = GeneratorTestHarness.RunGenerator(source, openApi);

        result.Diagnostics.Select(static d => d.Id).Should().Contain("GEREN015");
    }
}

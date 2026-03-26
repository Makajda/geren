namespace Geren.Client.Generator.Tests.Unit.Parse;

public sealed class ReturnTypeResolverTests {
    [Fact]
    public void Resolve_Prefers200Over201() {
        const string json = """
            {
              "openapi": "3.0.1",
              "info": { "title": "t", "version": "1.0" },
              "paths": {
                "/pets": {
                  "get": {
                    "operationId": "GetPets",
                    "responses": {
                      "201": { "description": "created", "content": { "text/plain": { "schema": { "type": "string" } } } },
                      "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "integer", "format": "int32" } } } }
                    }
                  }
                }
              }
            }
            """;

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var doc = OpenApiDocument.Load(ms).Document!;
        var op = doc.Paths["/pets"].Operations![HttpMethod.Get];

        var resolved = ReturnTypeResolver.Resolve(op);

        resolved.Name.Should().Be("int");
    }

    [Fact]
    public void Resolve_2xxWildcard_IsRecognizedCaseInsensitive() {
        const string json = """
            {
              "openapi": "3.0.1",
              "info": { "title": "t", "version": "1.0" },
              "paths": {
                "/ping": {
                  "get": {
                    "operationId": "Ping",
                    "responses": {
                      "2xx": { "description": "ok", "content": { "text/plain": { "schema": { "type": "string" } } } }
                    }
                  }
                }
              }
            }
            """;

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var doc = OpenApiDocument.Load(ms).Document!;
        var op = doc.Paths["/ping"].Operations![HttpMethod.Get];

        ReturnTypeResolver.Resolve(op).Name.Should().Be("string");
    }

    [Fact]
    public void Resolve_TextPlain_ReturnsString() {
        const string json = """
            {
              "openapi": "3.0.1",
              "info": { "title": "t", "version": "1.0" },
              "paths": {
                "/ping": {
                  "get": {
                    "operationId": "Ping",
                    "responses": {
                      "200": { "description": "ok", "content": { "text/plain": { "schema": { "type": "string" } } } }
                    }
                  }
                }
              }
            }
            """;

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var doc = OpenApiDocument.Load(ms).Document!;
        var op = doc.Paths["/ping"].Operations![HttpMethod.Get];

        var resolved = ReturnTypeResolver.Resolve(op);

        resolved.Name.Should().Be("string");
    }

    [Fact]
    public void Resolve_2xxWithoutPayload_FallsBackToDefaultWithPayload() {
        const string json = """
            {
              "openapi": "3.0.1",
              "info": { "title": "t", "version": "1.0" },
              "paths": {
                "/ping": {
                  "get": {
                    "operationId": "Ping",
                    "responses": {
                      "204": { "description": "no content" },
                      "default": { "description": "ok", "content": { "text/plain": { "schema": { "type": "string" } } } }
                    }
                  }
                }
              }
            }
            """;

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var doc = OpenApiDocument.Load(ms).Document!;
        var op = doc.Paths["/ping"].Operations![HttpMethod.Get];

        ReturnTypeResolver.Resolve(op).Name.Should().Be("string");
    }

    [Fact]
    public void Resolve_NoPayload_ReturnsEmptyType() {
        const string json = """
            {
              "openapi": "3.0.1",
              "info": { "title": "t", "version": "1.0" },
              "paths": {
                "/ping": {
                  "get": {
                    "operationId": "Ping",
                    "responses": {
                      "204": { "description": "no content" }
                    }
                  }
                }
              }
            }
            """;

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var doc = OpenApiDocument.Load(ms).Document!;
        var op = doc.Paths["/ping"].Operations![HttpMethod.Get];

        ReturnTypeResolver.Resolve(op).Name.Should().BeEmpty();
    }
}

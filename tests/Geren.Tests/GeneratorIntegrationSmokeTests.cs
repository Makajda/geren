using System.Collections.Immutable;
using System.Net;
using System.Reflection;
using System.Text;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Geren.Tests;

public sealed class GeneratorIntegrationSmokeTests {
    [Fact]
    public async Task Smoke_Runtime_Get_Encodes_Path_And_Query() {
        var source = "public sealed class Marker { }";
        var openApi = """
{
  "openapi": "3.0.4",
  "info": { "title": "t", "version": "1" },
  "paths": {
    "/orders/{id}": {
      "get": {
        "parameters": [
          { "name": "id", "in": "path", "required": true, "schema": { "type": "integer", "format": "int32" } },
          { "name": "include", "in": "query", "required": true, "schema": { "type": "boolean" } },
          { "name": "search", "in": "query", "required": false, "schema": { "type": "string" } }
        ],
        "responses": {
          "200": {
            "description": "ok",
            "content": {
              "text/plain": { "schema": { "type": "string" } }
            }
          }
        }
      }
    }
  }
}
""";

        var generation = GeneratorTestHarness.RunGenerator(source, openApi);
        generation.Diagnostics.Should().NotContain(static d => d.Severity == DiagnosticSeverity.Error);

        var compiled = GeneratorTestHarness.CompileGeneratedClientAssembly(source, generation.GeneratedSources);
        compiled.Diagnostics.Should().NotContain(static d => d.Severity == DiagnosticSeverity.Error);
        compiled.Assembly.Should().NotBeNull();

        var handler = new CapturingHandler(static _ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("ok", Encoding.UTF8, "text/plain")
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
        var client = CreateClient(compiled.Assembly!, "Gereb.Generated.V1.Root", http);

        var result = await InvokeStringMethodAsync(
            client,
            "GetOrders",
            [42, true, "hello world", CancellationToken.None]);

        result.Should().Be("ok");
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.OriginalString.Should().Be("https://api.test/orders/42?include=true&search=hello%20world");
    }

    [Fact]
    public async Task Smoke_Runtime_Delete_With_Json_Body_Uses_HttpRequestMessage() {
        var source = "public sealed class Marker { }";
        var openApi = """
{
  "openapi": "3.0.4",
  "info": { "title": "t", "version": "1" },
  "paths": {
    "/items/{id}": {
      "delete": {
        "parameters": [
          { "name": "id", "in": "path", "required": true, "schema": { "type": "integer", "format": "int32" } }
        ],
        "requestBody": {
          "required": true,
          "content": {
            "application/json": { "schema": { "type": "object" } }
          }
        },
        "responses": {
          "200": {
            "description": "ok",
            "content": {
              "text/plain": { "schema": { "type": "string" } }
            }
          }
        }
      }
    }
  }
}
""";

        var generation = GeneratorTestHarness.RunGenerator(source, openApi);
        generation.Diagnostics.Should().NotContain(static d => d.Severity == DiagnosticSeverity.Error);

        var compiled = GeneratorTestHarness.CompileGeneratedClientAssembly(source, generation.GeneratedSources);
        compiled.Diagnostics.Should().NotContain(static d => d.Severity == DiagnosticSeverity.Error);
        compiled.Assembly.Should().NotBeNull();

        var handler = new CapturingHandler(static _ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("deleted", Encoding.UTF8, "text/plain")
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
        var client = CreateClient(compiled.Assembly!, "Gereb.Generated.V1.Root", http);

        var result = await InvokeStringMethodAsync(
            client,
            "DeleteItems",
            [7, new { Name = "book" }, CancellationToken.None]);

        result.Should().Be("deleted");
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.ToString().Should().Be("https://api.test/items/7");
        handler.LastContentType.Should().Be("application/json");
        handler.LastBody.Should().Contain("book");
    }

    [Fact]
    public void Smoke_Generates_For_Multiple_OpenApi_Files() {
        var source = "public sealed class Marker { }";
        var v1 = """
{
  "openapi": "3.0.4",
  "info": { "title": "v1", "version": "1" },
  "paths": {
    "/status": {
      "get": {
        "responses": {
          "204": { "description": "no content" }
        }
      }
    }
  }
}
""";

        var v2 = """
{
  "openapi": "3.0.4",
  "info": { "title": "v2", "version": "1" },
  "paths": {
    "/health": {
      "get": {
        "responses": {
          "204": { "description": "no content" }
        }
      }
    }
  }
}
""";

        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText("v1.json", v1),
            new InMemoryAdditionalText("v2.json", v2));

        var generation = GeneratorTestHarness.RunGenerator(source, additionalTexts);
        generation.Diagnostics.Should().NotContain(static d => d.Severity == DiagnosticSeverity.Error);

        generation.GeneratedSources.Should().Contain(static s => s.HintName.EndsWith(".V1.Root.g.cs", StringComparison.Ordinal));
        generation.GeneratedSources.Should().Contain(static s => s.HintName.EndsWith(".V2.Root.g.cs", StringComparison.Ordinal));
        generation.GeneratedSources.Count(static s => s.HintName.EndsWith("FactoryBridge.g.cs", StringComparison.Ordinal))
            .Should().Be(1);

        var compiled = GeneratorTestHarness.CompileGeneratedClientAssembly(source, generation.GeneratedSources);
        compiled.Diagnostics.Should().NotContain(static d => d.Severity == DiagnosticSeverity.Error);
        compiled.Assembly.Should().NotBeNull();
        compiled.Assembly!.GetType("Gereb.Generated.V1.Root", throwOnError: false).Should().NotBeNull();
        compiled.Assembly!.GetType("Gereb.Generated.V2.Root", throwOnError: false).Should().NotBeNull();
    }

    private static object CreateClient(Assembly assembly, string typeName, HttpClient httpClient) {
        var type = assembly.GetType(typeName, throwOnError: true)!;
        var instance = Activator.CreateInstance(type, httpClient);
        instance.Should().NotBeNull();
        return instance!;
    }

    private static async Task<string> InvokeStringMethodAsync(object instance, string methodName, object[] args) {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull($"{instance.GetType().FullName} should contain method {methodName}");

        var invocationResult = method!.Invoke(instance, args);
        invocationResult.Should().BeAssignableTo<Task<string>>();
        return await (Task<string>)invocationResult!;
    }

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }
        public string? LastContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            LastRequest = request;
            if (request.Content is not null) {
                LastContentType = request.Content.Headers.ContentType?.MediaType;
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            } else {
                LastContentType = null;
                LastBody = null;
            }

            return responder(request);
        }
    }
}

namespace Geren.Client.Generator.Emit;

internal sealed class EmitClient {
    internal static string Run(IGrouping<object, Mapoint> endpoints, string rootNamespace, string spaceName, string className) => $$"""
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using static {{rootNamespace}}.FactoryBridge;

namespace {{spaceName}};

public sealed partial class {{className}} : GerenClientBase
{
    public {{className}}(HttpClient http) : base(http) { }

{{string.Join(Given.NewLine + Given.NewLine, endpoints.Select(EmitMethod))}}
}
""";

    private static string EmitMethod(Mapoint endpoint) {
        var args = string.Join(", ", endpoint.Params.Concat(endpoint.Queries).Select(p => $"{p.Type} {p.Identifier ?? p.Name}"));
        if (args.Length > 0)
            args += ", ";

        if (endpoint.Method is Givens.Post or Givens.Put or Givens.Patch)
            args += $"{endpoint.BodyType ?? "string"} body, ";

        var signature = $"{endpoint.MethodName}({args}CancellationToken cancellationToken = default)";
        var pathExpr = BuildPathExpression(endpoint);
        if (endpoint.Method is Givens.Get or Givens.Delete)
            return EmitGetDelete(endpoint, signature, pathExpr);

        return EmitPostOrPutOrPatch(endpoint, signature, pathExpr);
    }

    private static string EmitGetDelete(Mapoint endpoint, string signature, string pathExpr) {
        // void
        if (string.IsNullOrEmpty(endpoint.ReturnType)) {
            return $$"""
    public async Task {{signature}}
    {
        var path = {{pathExpr}};
        using var request = new HttpRequestMessage({{HttpMethodExpr(endpoint)}}, path);
        PrepareRequest(request);

        using var response = await Http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
""";
        }

        // ReadAsString
        if (endpoint.ReturnType == "string") {
            return $$"""
    public async Task<string> {{signature}}
    {
        var path = {{pathExpr}};
        using var request = new HttpRequestMessage({{HttpMethodExpr(endpoint)}}, path);
        PrepareRequest(request);

        using var response = await Http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
""";
        }

        // FromJson
        return $$"""
    public async Task<{{endpoint.ReturnType}}> {{signature}}
    {
        var path = {{pathExpr}};
        using var request = new HttpRequestMessage({{HttpMethodExpr(endpoint)}}, path);
        PrepareRequest(request);

        using var response = await Http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<{{endpoint.ReturnType}}>(JsonOptions, cancellationToken);
    }
""";
    }

    private static string EmitPostOrPutOrPatch(Mapoint endpoint, string signature, string pathExpr) {
        string method = HttpMethodExpr(endpoint);

        if (endpoint.BodyMedia == MediaTypes.Application_Json) {
            return EmitResponse(signature, endpoint.ReturnType, $$"""
        var path = {{pathExpr}};
        using var request = new HttpRequestMessage({{method}}, path)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        PrepareRequest(request);

        using var response = await Http.SendAsync(request, cancellationToken);
""");
        }

        // text/plain
        return EmitResponse(signature, endpoint.ReturnType, $$"""
        var path = {{pathExpr}};
        using var request = new HttpRequestMessage({{method}}, path)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain")
        };
        PrepareRequest(request);

        using var response = await Http.SendAsync(request, cancellationToken);
""");
    }

    private static string EmitResponse(string signature, string? returnType, string send) {
        // void
        if (string.IsNullOrEmpty(returnType)) {
            return $$"""
    public async Task {{signature}}
    {
{{send}}
        response.EnsureSuccessStatusCode();
    }
""";
        }

        // ReadAsString
        if (returnType == "string") {
            return $$"""
    public async Task<string> {{signature}}
    {
{{send}}
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
""";
        }

        // FromJson
        return $$"""
    public async Task<{{returnType}}> {{signature}}
    {
{{send}}
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<{{returnType}}>(JsonOptions, cancellationToken);
    }
""";
    }
    private static string BuildPathExpression(Mapoint endpoint) {
        string interpolatedPath = endpoint.Path;
        foreach (var param in endpoint.Params)
            interpolatedPath = interpolatedPath.Replace("{" + param.Name + "}", "{V(" + (param.Identifier ?? param.Name) + ")}");

        string pathExpr = $"$\"{interpolatedPath}\"";
        if (endpoint.Queries.IsEmpty)
            return pathExpr;

        string queryBuilder = string.Join(Given.NewLine, endpoint.Queries.Select(static p =>
            $"            A(query, \"{p.Name}\", {p.Identifier});"));

        return $$"""
BuildRequestUri({{pathExpr}}, query =>
        {
{{queryBuilder}}
        })
""";
    }

    private static string HttpMethodExpr(Mapoint endpoint)
        => endpoint.Method switch
        {
            Givens.Get => "HttpMethod.Get",
            Givens.Post => "HttpMethod.Post",
            Givens.Put => "HttpMethod.Put",
            Givens.Patch => "HttpMethod.Patch",
            Givens.Delete => "HttpMethod.Delete",
            _ => "new HttpMethod(\"" + endpoint.Method + "\")",
        };
}

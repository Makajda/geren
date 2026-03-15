namespace Geren.Generator.Emit;

internal sealed class EmitClient {
    internal static string Run(IGrouping<object, EndpointSpec> endpoints, string rootNamespace, string spaceName, string className) => $$"""
using System.Net.Http.Json;
using static {{rootNamespace}}.FactoryBridge;

namespace {{spaceName}};

public sealed partial class {{className}}
{
    private readonly HttpClient _http;
    public {{className}}(HttpClient http) => _http = http;

{{string.Join(Givenn.NewLine + Givenn.NewLine, endpoints.Select(EmitMethod))}}
}
""";

    private static string EmitMethod(EndpointSpec endpoint) {
        var methodName = endpoint.MethodName;
        var args = string.Join(", ", endpoint.Params.Concat(endpoint.Queries).Select(p => $"{p.TypeName} {p.Identifier}"));
        if (args.Length > 0)
            args += ", ";

        if (endpoint.BodyType is not null && (endpoint.Method == Givenn.Post || endpoint.Method == Givenn.Put || endpoint.Method == Givenn.Delete))
            args += $"{endpoint.BodyType} body, ";

        var signature = $"{methodName}({args}CancellationToken cancellationToken = default)";
        var pathExpr = BuildPathExpression(endpoint);
        if (endpoint.Method == Givenn.Get)
            return EmitGet(endpoint.ReturnType, signature, pathExpr);

        if (endpoint.Method == Givenn.Delete)
            return EmitDelete(endpoint, signature, pathExpr);

        return EmitPostOrPut(endpoint, signature, pathExpr);
    }

    private static string EmitGet(string returnType, string signature, string pathExpr) {
        // void
        if (string.IsNullOrEmpty(returnType)) {
            return $$"""
    public async Task {{signature}}
    {
        var response = await _http.GetAsync({{pathExpr}}, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
""";
        }

        // ReadAsString
        if (returnType == "string") {
            return $$"""
    public async Task<string> {{signature}}
    {
        var response = await _http.GetAsync({{pathExpr}}, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
""";
        }

        // FromJson
        return $$"""
    public Task<{{returnType}}> {{signature}}
        => _http.GetFromJsonAsync<{{returnType}}>({{pathExpr}}, cancellationToken);
""";
    }

    private static string EmitDelete(EndpointSpec e, string signature, string pathExpr) {
        string send;
        if (e.BodyMediaType == "application/json")
            send = $$"""
        using var request = new HttpRequestMessage(HttpMethod.Delete, {{pathExpr}})
        {
            Content = JsonContent.Create(body)
        };
        var response = await _http.SendAsync(request, cancellationToken);
""";
        else if (e.BodyMediaType == "text/plain")
            send = $$"""
        using var request = new HttpRequestMessage(HttpMethod.Delete, {{pathExpr}})
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain")
        };
        var response = await _http.SendAsync(request, cancellationToken);
""";
        else
            send = $"        var response = await _http.DeleteAsync({pathExpr}, cancellationToken);";

        return EmitResponse(signature, e.ReturnType, send);
    }

    private static string EmitPostOrPut(EndpointSpec e, string signature, string pathExpr) {
        string send;
        if (e.BodyMediaType == "application/json")
            send = $"        var response = await _http.{e.Method}AsJsonAsync({pathExpr}, body, cancellationToken);";
        else if (e.BodyMediaType == "text/plain")
            send = $$"""
        using var content = new StringContent(body, Encoding.UTF8, "text/plain");
        var response = await _http.{{e.Method}}Async({{pathExpr}}, content, cancellationToken);
""";
        else
            send = $"        var response = await _http.{e.Method}Async({pathExpr}, null, cancellationToken);";

        return EmitResponse(signature, e.ReturnType, send);
    }

    private static string EmitResponse(string signature, string returnType, string send) {
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
        return await response.Content.ReadFromJsonAsync<{{returnType}}>(cancellationToken);
    }
""";
    }
    private static string BuildPathExpression(EndpointSpec endpoint) {
        string interpolatedPath = endpoint.Path;
        foreach (var param in endpoint.Params)
            interpolatedPath = interpolatedPath.Replace("{" + param.Name + "}", "{V(" + param.Identifier + ")}");

        string pathExpr = $"$\"{interpolatedPath}\"";
        if (endpoint.Queries.Length == 0)
            return pathExpr;

        string queryBuilder = string.Join(Givenn.NewLine, endpoint.Queries.Select(static p =>
            $"            A(query, \"{p.Name}\", {p.Identifier});"));

        return $$"""
BuildRequestUri({{pathExpr}}, query =>
        {
{{queryBuilder}}
        })
""";
    }
}

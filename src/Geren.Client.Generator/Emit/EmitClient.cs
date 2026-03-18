namespace Geren.Client.Generator.Emit;

internal sealed class EmitClient {
    internal static string Run(IGrouping<object, Mapoint> endpoints, string rootNamespace, string spaceName, string className) => $$"""
using System.Net.Http.Json;
using static {{rootNamespace}}.FactoryBridge;

namespace {{spaceName}};

public sealed partial class {{className}}
{
    private readonly HttpClient _http;
    public {{className}}(HttpClient http) => _http = http;

{{string.Join(Given.NewLine + Given.NewLine, endpoints.Select(EmitMethod))}}
}
""";

    private static string EmitMethod(Mapoint endpoint) {
        var (point, returnType, bodyType, _) = endpoint;
        var methodName = point.MethodName;
        var args = string.Join(", ", endpoint.Params.Concat(point.Queries).Select(p => $"{p.Type} {p.Identifier}"));
        if (args.Length > 0)
            args += ", ";

        if (bodyType is not null && (point.Method == Given.Post || point.Method == Given.Put || point.Method == Given.Delete))
            args += $"{bodyType} body, ";

        var signature = $"{methodName}({args}CancellationToken cancellationToken = default)";
        var pathExpr = BuildPathExpression(endpoint);
        if (point.Method == Given.Get)
            return EmitGet(returnType, signature, pathExpr);

        if (point.Method == Given.Delete)
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

    private static string EmitDelete(Mapoint endpoint, string signature, string pathExpr) {
        string send;
        if (endpoint.Point.BodyMediaType == "application/json")
            send = $$"""
        using var request = new HttpRequestMessage(HttpMethod.Delete, {{pathExpr}})
        {
            Content = JsonContent.Create(body)
        };
        var response = await _http.SendAsync(request, cancellationToken);
""";
        else if (endpoint.Point.BodyMediaType == "text/plain")
            send = $$"""
        using var request = new HttpRequestMessage(HttpMethod.Delete, {{pathExpr}})
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain")
        };
        var response = await _http.SendAsync(request, cancellationToken);
""";
        else
            send = $"        var response = await _http.DeleteAsync({pathExpr}, cancellationToken);";

        return EmitResponse(signature, endpoint.ReturnType, send);
    }

    private static string EmitPostOrPut(Mapoint endpoint, string signature, string pathExpr) {
        var (point, returnType, _, _) = endpoint;
        string send;
        if (point.BodyMediaType == "application/json")
            send = $"        var response = await _http.{point.Method}AsJsonAsync({pathExpr}, body, cancellationToken);";
        else if (point.BodyMediaType == "text/plain")
            send = $$"""
        using var content = new StringContent(body, Encoding.UTF8, "text/plain");
        var response = await _http.{{point.Method}}Async({{pathExpr}}, content, cancellationToken);
""";
        else
            send = $"        var response = await _http.{point.Method}Async({pathExpr}, null, cancellationToken);";

        return EmitResponse(signature, returnType, send);
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
    private static string BuildPathExpression(Mapoint endpoint) {
        string interpolatedPath = endpoint.Point.Path;
        foreach (var param in endpoint.Params)
            interpolatedPath = interpolatedPath.Replace("{" + param.Name + "}", "{V(" + param.Identifier + ")}");

        string pathExpr = $"$\"{interpolatedPath}\"";
        if (endpoint.Point.Queries.Length == 0)
            return pathExpr;

        string queryBuilder = string.Join(Given.NewLine, endpoint.Point.Queries.Select(static p =>
            $"            A(query, \"{p.Name}\", {p.Identifier});"));

        return $$"""
BuildRequestUri({{pathExpr}}, query =>
        {
{{queryBuilder}}
        })
""";
    }
}

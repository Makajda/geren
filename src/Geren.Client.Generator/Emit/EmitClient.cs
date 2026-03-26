namespace Geren.Client.Generator.Emit;

internal sealed class EmitClient {
    internal static string Run(IGrouping<object, Mapoint> endpoints, string rootNamespace, string spaceName, string className) => $$"""
using System.Net.Http.Json;
using System.Text;
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
        var methodName = endpoint.MethodName;
        var args = string.Join(", ", endpoint.Params.Concat(endpoint.Queries).Select(p => $"{p.Type} {p.Identifier}"));
        if (args.Length > 0)
            args += ", ";

        if (!string.IsNullOrEmpty(endpoint.BodyType) && (endpoint.Method == Given.Post
            || endpoint.Method == Given.Put
            || endpoint.Method == Given.Patch
            || endpoint.Method == Given.Delete))
            args += $"{endpoint.BodyType} body, ";

        var signature = $"{methodName}({args}CancellationToken cancellationToken = default)";
        var pathExpr = BuildPathExpression(endpoint);
        if (endpoint.Method == Given.Get || endpoint.Method == Given.Delete)
            return EmitGetDelete(endpoint, signature, pathExpr);

        return EmitPostOrPutOrPatch(endpoint, signature, pathExpr);
    }

    private static string EmitGetDelete(Mapoint endpoint, string signature, string pathExpr) {
        // void
        if (string.IsNullOrEmpty(endpoint.ReturnType)) {
            return $$"""
    public async Task {{signature}}
    {
        var response = await _http.{{endpoint.Method}}Async({{pathExpr}}, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
""";
        }

        // ReadAsString
        if (endpoint.ReturnType == "string") {
            return $$"""
    public async Task<string> {{signature}}
    {
        var response = await _http.{{endpoint.Method}}Async({{pathExpr}}, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
""";
        }

        // FromJson
        return $$"""
    public Task<{{endpoint.ReturnType}}> {{signature}}
        => _http.{{endpoint.Method}}FromJsonAsync<{{endpoint.ReturnType}}>({{pathExpr}}, cancellationToken);
""";
    }

    private static string EmitPostOrPutOrPatch(Mapoint endpoint, string signature, string pathExpr) {
        string send;
        if (endpoint.BodyMedia == MediaTypes.Application_Json)
            send = $"        var response = await _http.{endpoint.Method}AsJsonAsync({pathExpr}, body, cancellationToken);";
        else
            send = $$"""
        using var content = new StringContent(body, Encoding.UTF8, "text/plain");
        var response = await _http.{{endpoint.Method}}Async({{pathExpr}}, content, cancellationToken);
""";

        return EmitResponse(signature, endpoint.ReturnType, send);
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
        return await response.Content.ReadFromJsonAsync<{{returnType}}>(cancellationToken);
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
}

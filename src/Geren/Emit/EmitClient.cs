namespace Geren.Emit;

internal sealed class EmitClient {
    internal static string Run(IGrouping<object, EndpointSpec> endpoints, string spaceName, string className) => $$"""
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        args += "CancellationToken cancellationToken = default";
        var signature = $"{methodName}({args})";
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

    private static string BuildPathExpression(EndpointSpec e) {
        string interpolatedPath = e.Path;
        foreach (var param in e.Params)
            interpolatedPath = interpolatedPath.Replace("{" + param.Name + "}", "{" + param.Identifier + "}");

        if (e.Queries.Length == 0)
            return $"$\"{interpolatedPath}\"";

        // Query
        string queryBuilder = string.Join(Givenn.NewLine, e.Queries.Select(p =>
            $"        query.Add(\"{p.Name}=\" + Uri.EscapeDataString({BuildQueryValueExpression(p)}));"));

        string result = $"new Func<string>(() =>{Givenn.NewLine}"
            + "    {" + Givenn.NewLine
            + "        var query = new List<string>();" + Givenn.NewLine
            + queryBuilder + Givenn.NewLine
            + $"        return $\"{interpolatedPath}?{{string.Join(\"&\", query)}}\";{Givenn.NewLine}"
            + "    })()";

        return result;
    }

    private static string BuildQueryValueExpression(ParamSpec param) {
        if (param.TypeName == "bool")
            return $"{param.Identifier} ? \"true\" : \"false\"";

        return $"Convert.ToString({param.Identifier}, CultureInfo.InvariantCulture) ?? string.Empty";
    }
}

namespace Geren.Emit;

internal sealed class EmitClients {
    internal static ImmutableArray<(string Name, string Code)> Run(MapInc map, string rootNamespace) {
        var classes = map.Endpoints.GroupBy(e => e.ClassName, StringComparer.Ordinal);
        var result = ImmutableArray.CreateBuilder<(string, string)>();

        foreach (var g in classes) {
            string name = g.Key;
            var methods = string.Join(Givenn.NewLine + Givenn.NewLine, g.Select(EmitMethod));
            var code = $$"""
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace {{rootNamespace}}.{{map.NamespaceSuffix}};

public sealed class {{name}}
{
    private readonly HttpClient _http;
    public {{name}}(HttpClient http) => _http = http;

{{methods}}
}
""";
            result.Add(($"{name}.g.cs", code));
        }

        return result.ToImmutable();
    }

    private static string EmitMethod(EndpointSpec e) {
        var methodName = e.MethodName;
        var args = string.Join(", ", e.Params.Concat(e.Queries).Select(p => $"{p.TypeName} {p.Identifier}"));
        if (args.Length > 0)
            args += ", ";

        if (e.BodyType is not null && (e.Method == Givenn.Post || e.Method == Givenn.Put || e.Method == Givenn.Delete))
            args += $"{e.BodyType} body, ";

        args += "CancellationToken cancellationToken = default";
        var signature = $"{methodName}({args})";
        var pathExpr = BuildPathExpression(e);

        if (e.Method == Givenn.Get)
            return EmitGet(e.ReturnType, signature, pathExpr);

        if (e.Method == Givenn.Delete)
            return EmitDelete(e, signature, pathExpr);

        return EmitPostOrPut(e, signature, pathExpr);
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
    public Task<{{returnType}}> {{signature}} {
        var payload = _http.GetFromJsonAsync<{{returnType}}>({{pathExpr}}, cancellationToken);
        if (payload is null)
            throw new InvalidOperationException("Generated client '{{GetMethodName(signature)}}' received null body for non-string response.");
        return payload;
    }
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

    private static string BuildPathExpression(EndpointSpec e) {
        var interpolatedPath = BuildInterpolatedPath(e.Path, e.Params);
        if (e.Queries.Length == 0)
            return $"$\"{interpolatedPath}\"";

        var queryBuilder = string.Join(Givenn.NewLine, e.Queries.Select(p =>
            $"        query.Add(\"{p.Name}=\" + Uri.EscapeDataString({BuildQueryValueExpression(p)}));"));

        var text = $"new Func<string>(() =>{Givenn.NewLine}"
            + "    {" + Givenn.NewLine
            + "        var query = new List<string>();" + Givenn.NewLine
            + queryBuilder + Givenn.NewLine
            + $"        return $\"{interpolatedPath}?{{string.Join(\"&\", query)}}\";{Givenn.NewLine}"
            + "    })()";

        return text;
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
        var payload = await response.Content.ReadFromJsonAsync<{{returnType}}>(cancellationToken);
        if (payload is null)
            throw new InvalidOperationException("Generated client '{{GetMethodName(signature)}}' received null body for non-string response.");
        return payload;
    }
""";
    }

    private static string BuildInterpolatedPath(string path, ImmutableArray<ParamSpec> pathParams) {
        var result = path;
        foreach (var param in pathParams)
            result = result.Replace(
                "{" + param.Name + "}",
                "{Uri.EscapeDataString(Convert.ToString(" + param.Identifier + ", CultureInfo.InvariantCulture) ?? string.Empty)}");

        return result;
    }

    private static string BuildQueryValueExpression(ParamSpec param) {
        if (param.TypeName == "bool")
            return $"{param.Identifier} ? \"true\" : \"false\"";

        return $"Convert.ToString({param.Identifier}, CultureInfo.InvariantCulture) ?? string.Empty";
    }

    private static object GetMethodName(string signature) {
        int index = signature.IndexOf('(');
        return index >= 0 ? signature.Substring(0, index) : signature;
    }
}

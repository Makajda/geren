namespace Geren.Map;

internal static class OperationReturnType {
    internal static string Resolve(OpenApiOperation operation, SchemaTypeName schemaTypeName) {
        foreach (var responseEntry in operation.Responses
            .Where(static r => Is2xxStatusCode(r.Key))
            .OrderBy(static r => GetResponsePriority(r.Key))
            .ThenBy(static r => r.Key, StringComparer.Ordinal)) {
            var resolved = ResolveResponsePayloadType(responseEntry.Value, schemaTypeName);
            if (schemaTypeName.HasFatalEndpointError)
                return string.Empty;

            return resolved;
        }

        foreach (var fallbackCode in new[] { "200", "201", "default" }) {
            if (operation.Responses is null || !TryGetResponseByCode(operation.Responses, fallbackCode, out var fallback))
                continue;

            var resolved = ResolveResponsePayloadType(fallback, schemaTypeName);
            if (!schemaTypeName.HasFatalEndpointError)
                return resolved;
        }

        return string.Empty;
    }

    private static string ResolveResponsePayloadType(IOpenApiResponse response, SchemaTypeName schemaTypeName) {
        if (response.Content is null || response.Content.Count == 0)
            return string.Empty;

        if (response.Content.TryGetValue("application/json", out IOpenApiMediaType json))
            return schemaTypeName.Resolve(json.Schema);

        if (response.Content.ContainsKey("text/plain"))
            return "string";

        return string.Empty;
    }

    private static bool Is2xxStatusCode(string code) {
        if (string.Equals(code, "2XX", StringComparison.OrdinalIgnoreCase))
            return true;

        return code.Length == 3
            && code[0] == '2'
            && char.IsDigit(code[1])
            && char.IsDigit(code[2]);
    }

    private static int GetResponsePriority(string code) {
        if (string.Equals(code, "200", StringComparison.Ordinal))
            return 0;
        if (string.Equals(code, "201", StringComparison.Ordinal))
            return 1;
        if (string.Equals(code, "202", StringComparison.Ordinal))
            return 2;
        if (string.Equals(code, "2XX", StringComparison.OrdinalIgnoreCase))
            return 1000;
        if (code.Length == 3 && int.TryParse(code, out var numeric))
            return 100 + numeric;

        return int.MaxValue;
    }

    private static bool TryGetResponseByCode(OpenApiResponses responses, string code, out IOpenApiResponse response) {
        if (responses.TryGetValue(code, out response))
            return true;

        foreach (var item in responses) {
            if (!string.Equals(item.Key, code, StringComparison.OrdinalIgnoreCase))
                continue;

            response = item.Value;
            return true;
        }

        response = null!;
        return false;
    }
}

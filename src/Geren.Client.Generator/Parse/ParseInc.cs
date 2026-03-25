namespace Geren.Client.Generator.Parse;

internal sealed record ParseInc(
    bool Success,
    string FilePath,
    ImmutableArray<Purpoint> Purpoints,
    ImmutableArray<Diagnostic> Diagnostics) {

    private static ParseInc Skip(Diagnostic diagnostic) => new(false, string.Empty, [], [diagnostic]);

    internal static ParseInc Parse(AdditionalText file, string jsonFormat, CancellationToken cancellationToken) {
        string? text = file.GetText(cancellationToken)?.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return Skip(Diagnostic.Create(Dide.JsonReadError, Location.None, $"Invalid {file.Path}: File is empty."));

        if (string.Equals(jsonFormat, "gerenapi", StringComparison.OrdinalIgnoreCase))
            return ParseGerenApi(file.Path, text!, cancellationToken);
        else
            return ParseOpenApi(file.Path, text!, cancellationToken);
    }

    private static ParseInc ParseGerenApi(string filePath, string text, CancellationToken cancellationToken) {
        return new(true, filePath, [new(Given.Get, "/gerenapi", "GerenApi", "GerenApiClient", "GetData", new("string"), null, null, [], [])], []);
    }

    private static ParseInc ParseOpenApi(string filePath, string text, CancellationToken cancellationToken) {
        try {
            using MemoryStream ms = new(Encoding.UTF8.GetBytes(Given.ArraysDisguise(text!)));
            var readResult = OpenApiDocument.Load(ms);
            var errors = readResult.Diagnostic?.Errors;
            if (errors is not null && errors.Any())
                return Skip(Diagnostic.Create(Dide.ParseError, Location.None,
                    $"OpenAPI errors in {filePath}: {string.Join("; ", errors.Select(e => e.Message))}"));

            OpenApiDocument? document = readResult.Document;
            if (document is null)
                return Skip(Diagnostic.Create(Dide.ParseError, Location.None, $"OpenAPI reader returned null for {filePath}"));

            return Build(filePath, document, cancellationToken);
        }
        catch (Exception ex) {
            return Skip(Diagnostic.Create(Dide.ParseError, Location.None,
                $"OpenAPI parse exception in {filePath}: {ex.Message}"));
        }
    }

    private static ParseInc Build(string filePath, OpenApiDocument doc, CancellationToken cancellationToken) {
        var _endpoints = ImmutableArray.CreateBuilder<Purpoint>();
        var _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        HashSet<string> seenMethodKeys = new(StringComparer.Ordinal);
        foreach (var path in doc.Paths) {
            cancellationToken.ThrowIfCancellationRequested();

            string normalizedPath = NormalizePathTemplate(path.Key);
            if (path.Value?.Operations is null)
                continue;

            foreach (var operation in path.Value.Operations) {
                string key = operation.Key.ToString();
                if (key.Length < 3)
                    continue;

                string method = char.ToUpperInvariant(key[0]) + key.Substring(1).ToLowerInvariant();
                if (method != Given.Get && method != Given.Post && method != Given.Put && method != Given.Delete)
                    continue;

                var (spaceName, className, methodName) = ResolveNames(operation.Value.OperationId, method, normalizedPath);
                string methodKey = spaceName + "." + className + "." + methodName;
                if (!seenMethodKeys.Add(methodKey)) {
                    _diagnostics.Add(Diagnostic.Create(Dide.DuplicateMethodName, Location.None, methodName, className, path.Key));
                    continue;
                }

                var returnType = ReturnTypeResolver.Resolve(operation.Value);
                var (bodyType, bodyMediaType) = ResolveRequestBody(operation.Value, _diagnostics);
                if (bodyType is null && bodyMediaType is not null)
                    continue;

                var effectiveParameters = MergeOperationParameters(path.Value.Parameters, operation.Value.Parameters);
                var (@params, queries) = PathAndQueryParameters.Split(path.Key, effectiveParameters, _diagnostics);
                if (!ValidatePathParametersAgainstPath(path.Key, normalizedPath, @params, _diagnostics))
                    continue;

                _endpoints.Add(new(method, normalizedPath, spaceName, className, methodName, returnType, bodyType, bodyMediaType, @params, queries));
            }
        }

        return new(true, filePath, _endpoints.ToImmutable(), _diagnostics.ToImmutable());
    }

    private static (PurposeType? BodyType, string? MediaType) ResolveRequestBody(OpenApiOperation operation, ImmutableArray<Diagnostic>.Builder diagnostics) {
        var requestBody = operation.RequestBody;
        if (requestBody is null || requestBody.Content is null || requestBody.Content.Count == 0)
            return (null, null);

        if (requestBody.Content.TryGetValue("application/json", out var json))
            return (SchemaToPurpose.Convert(json.Schema), "application/json");

        if (requestBody.Content.ContainsKey("text/plain"))
            return (new("string"), "text/plain");

        string mediaType = requestBody.Content.Keys.FirstOrDefault() ?? "<unknown>";
        diagnostics.Add(Diagnostic.Create(Dide.UnsupportedRequestBody, Location.None, operation.OperationId, mediaType));
        return (null, mediaType);
    }

    private static bool ValidatePathParametersAgainstPath(
        string rawPath,
        string normalizedPath,
        ImmutableArray<Purparam> pathParams,
        ImmutableArray<Diagnostic>.Builder diagnostics) {
        var placeholders = ExtractPathPlaceholderNames(normalizedPath);
        if (placeholders.Count == 0 && pathParams.Length == 0)
            return true;

        var placeholderSet = new HashSet<string>(placeholders, StringComparer.Ordinal);
        var parameterSet = new HashSet<string>(pathParams.Select(static p => p.Name), StringComparer.Ordinal);
        if (placeholderSet.SetEquals(parameterSet))
            return true;

        var missingParameters = placeholders
            .Where(name => !parameterSet.Contains(name))
            .ToArray();
        var extraParameters = pathParams
            .Select(static p => p.Name)
            .Where(name => !placeholderSet.Contains(name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var details = new List<string>(2);
        if (missingParameters.Length > 0)
            details.Add("placeholders without path parameter: " + string.Join(", ", missingParameters));
        if (extraParameters.Length > 0)
            details.Add("path parameters not found in path: " + string.Join(", ", extraParameters));

        diagnostics.Add(Diagnostic.Create(Dide.PathParameterNameMismatch, Location.None, rawPath, string.Join("; ", details)));
        return false;
    }

    private static string NormalizePathTemplate(string path) {
        if (string.IsNullOrEmpty(path))
            return path;

        var sb = new StringBuilder(path.Length);
        int i = 0;
        while (i < path.Length) {
            char ch = path[i];
            if (ch != '{') {
                sb.Append(ch);
                i++;
                continue;
            }

            int closeIndex = path.IndexOf('}', i + 1);
            if (closeIndex < 0) {
                sb.Append(path, i, path.Length - i);
                break;
            }

            string inner = path.Substring(i + 1, closeIndex - i - 1);
            var colonIndex = inner.IndexOf(':');
            if (colonIndex >= 0)
                inner = inner.Substring(0, colonIndex);

            sb.Append('{').Append(inner).Append('}');
            i = closeIndex + 1;
        }

        return sb.ToString();
    }

    private static (string SpaceName, string ClassName, string MethodName) ResolveNames(string? operationId, string method, string path) {
        string? withName = operationId is null ? null : Given.ToLetterOrDigitName(operationId);
        string[] sections = [.. path
            .Trim('/')
            .Split(['/'], StringSplitOptions.RemoveEmptyEntries)
            .Where(static s => !IsPathTemplateSegment(s))
            .Select(n=>Given.ToLetterOrDigitName(n))];

        int classIndex = sections.Length - 2;
        string methodName = withName ?? (method + sections.LastOrDefault());
        string className = classIndex >= 0 ? sections[classIndex] : "WebApiClient";
        string spaceName = classIndex > 0 ? string.Join(".", sections.Take(classIndex)) : string.Empty;
        return (spaceName, className, methodName);
    }

    private static bool IsPathTemplateSegment(string segment)
        => segment.Length > 1 && segment[0] == '{' && segment[segment.Length - 1] == '}';

    private static ImmutableArray<IOpenApiParameter> MergeOperationParameters(
        IList<IOpenApiParameter>? pathParameters,
        IList<IOpenApiParameter>? operationParameters) {
        List<IOpenApiParameter> merged = [];
        Dictionary<string, int> indexByKey = new(StringComparer.Ordinal);

        Add(pathParameters, allowOverride: false);
        Add(operationParameters, allowOverride: true);
        return [.. merged];

        void Add(IList<IOpenApiParameter>? source, bool allowOverride) {
            if (source is null)
                return;

            foreach (var parameter in source) {
                if (parameter.In is null) {
                    merged.Add(parameter);
                    continue;
                }

                string key = parameter.Name + "\u001F" + parameter.In.Value.ToString();

                if (allowOverride && indexByKey.TryGetValue(key, out int existingIndex)) {
                    merged[existingIndex] = parameter;
                    continue;
                }

                if (indexByKey.ContainsKey(key))
                    continue;

                indexByKey[key] = merged.Count;
                merged.Add(parameter);
            }
        }
    }

    private static List<string> ExtractPathPlaceholderNames(string path) {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int i = 0;
        while (i < path.Length) {
            int open = path.IndexOf('{', i);
            if (open < 0)
                break;

            int close = path.IndexOf('}', open + 1);
            if (close < 0)
                break;

            string name = path.Substring(open + 1, close - open - 1);
            if (name.Length > 0 && seen.Add(name))
                names.Add(name);

            i = close + 1;
        }

        return names;
    }
}

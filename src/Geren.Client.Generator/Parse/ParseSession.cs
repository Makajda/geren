using Microsoft.CodeAnalysis.CSharp;

namespace Geren.Client.Generator.Parse;

internal sealed class ParseSession {
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

    internal ParseInc BuildMap(string filePath, OpenApiDocument doc) {
        var endpoints = ImmutableArray.CreateBuilder<Purpoint>();
        HashSet<string> seenMethodKeys = new(StringComparer.Ordinal);
        foreach (var path in doc.Paths) {
            //todo cancellationToken
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

                var returnType = ReturnTypeResolver.Resolve(operation.Value, Resolve);
                var (bodyType, bodyMediaType) = ResolveRequestBody(operation.Value);
                if (bodyType is null && bodyMediaType is not null)
                    continue;

                var effectiveParameters = MergeOperationParameters(path.Value.Parameters, operation.Value.Parameters);
                var (@params, queries) = SplitPathAndQueryParameters(path.Key, effectiveParameters);
                if (!ValidatePathParametersAgainstPath(path.Key, normalizedPath, @params))
                    continue;

                endpoints.Add(new(method, normalizedPath, spaceName, className, methodName, returnType, bodyType, bodyMediaType, @params, queries));
            }
        }

        return new(true, filePath, endpoints.ToImmutable(), _diagnostics.ToImmutable());
    }

    internal PurposeType Resolve(IOpenApiSchema? schema) {
        const string defaultType = "string";
        if (schema is null)
            return new(defaultType);

        bool hasExtensions = schema.Extensions is not null;
        if (hasExtensions && schema.Extensions!.TryGetValue("x-metadata", out IOpenApiExtension nodeExtension)) {
            if (nodeExtension is JsonNodeExtension node)
                return new(node.Node.GetValue<string>(), PurposeTypes.Metadata);
        }
        else if (hasExtensions && schema.Extensions!.TryGetValue("x-compile", out IOpenApiExtension nodeGeneric)) {
            if (nodeGeneric is JsonNodeExtension node)
                return new(Given.ArraysRestore(node.Node.GetValue<string>()), PurposeTypes.Compile);
        }
        else if (schema is OpenApiSchemaReference schemaReference)
            if (schemaReference.Reference.Id is not null)
                return new(schemaReference.Reference.Id, PurposeTypes.Reference);

        if (schema.Format == "int64") return new("long");
        if (schema.Format == "int32") return new("int");

        if (schema.Type.HasValue) return new(schema.Type.Value switch {
            JsonSchemaType.Null => "string",
            JsonSchemaType.Boolean => "bool",
            JsonSchemaType.Integer => "int",
            JsonSchemaType.Number => "double",
            JsonSchemaType.String => "string",
            JsonSchemaType.Object => "object",
            JsonSchemaType.Array => $"System.Collections.Generic.IReadOnlyList<{Resolve(schema.Items)}>",
            _ => defaultType
        });
        return new(defaultType);
    }

    private (PurposeType? BodyType, string? MediaType) ResolveRequestBody(OpenApiOperation operation) {
        var requestBody = operation.RequestBody;
        if (requestBody is null || requestBody.Content is null || requestBody.Content.Count == 0)
            return (null, null);

        if (requestBody.Content.TryGetValue("application/json", out var json))
            return (Resolve(json.Schema), "application/json");

        if (requestBody.Content.ContainsKey("text/plain"))
            return (new("string"), "text/plain");

        string mediaType = requestBody.Content.Keys.FirstOrDefault() ?? "<unknown>";
        _diagnostics.Add(Diagnostic.Create(Dide.UnsupportedRequestBody, Location.None, operation.OperationId, mediaType));
        return (null, mediaType);
    }

    private (ImmutableArray<Purparam> Params, ImmutableArray<ParamSpec> Queries) SplitPathAndQueryParameters(
        string rawPath,
        ImmutableArray<IOpenApiParameter> parameters) {
        var pathParams = ImmutableArray.CreateBuilder<Purparam>();
        var queryParams = ImmutableArray.CreateBuilder<ParamSpec>();
        var usedParamIdentifiers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var parameter in parameters) {
            if (parameter is null || parameter.In is null || parameter.Name is null) {
                _diagnostics.Add(Diagnostic.Create(Dide.MissingParamLocation, Location.None, parameter?.Name ?? "<noname>", rawPath));
                continue;
            }

            string inValue = parameter.In.Value.ToString().ToLowerInvariant();
            if (inValue == "path") {
                string identifier = ToParameterIdentifier(parameter.Name, usedParamIdentifiers);
                PurposeType paramType = Resolve(parameter.Schema);
                pathParams.Add(new(parameter.Name, identifier, paramType));

                continue;
            }

            if (inValue == "query") {
                string identifier = ToParameterIdentifier(parameter.Name, usedParamIdentifiers);
                var (type, _) = Resolve(parameter.Schema);
                type = parameter.Required || type.EndsWith("?", StringComparison.Ordinal) ? type : type + "?";
                if (IsSupportedQueryType(type))
                    queryParams.Add(new(parameter.Name, identifier, type));
                else
                    _diagnostics.Add(Diagnostic.Create(Dide.UnsupportedQueryType, Location.None, parameter.Name, rawPath, type));

                continue;
            }

            _diagnostics.Add(Diagnostic.Create(Dide.UnsupportedParamLocation, Location.None, parameter.Name, inValue));
        }

        return (pathParams.ToImmutable(), queryParams.ToImmutable());
    }

    private bool ValidatePathParametersAgainstPath(string rawPath, string normalizedPath, ImmutableArray<Purparam> pathParams) {
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

        _diagnostics.Add(Diagnostic.Create(Dide.PathParameterNameMismatch, Location.None, rawPath, string.Join("; ", details)));
        return false;
    }

    //static
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
        const string spaceDefault = "";
        const string classNameDefault = "WebApiClient";
        const string methodNameDefault = "Root";
        string? withName = operationId is null ? null : Given.ToLetterOrDigitName(operationId);
        string[] sections = [.. path
            .Trim('/')
            .Split(['/'], StringSplitOptions.RemoveEmptyEntries)
            .Where(static s => !IsPathTemplateSegment(s))];
        if (sections.Length == 0) return (spaceDefault, classNameDefault, MethodName(methodNameDefault));
        if (sections.Length == 1) return (spaceDefault, classNameDefault, MethodName(sections[0]));
        string name0 = Given.ToLetterOrDigitName(sections[0]);
        if (sections.Length == 2) return (spaceDefault, name0, MethodName(sections[1]));
        return (name0, Given.ToLetterOrDigitName(sections[1]), MethodName(string.Join("_", sections.Skip(2))));

        string MethodName(string section) => withName ?? (method + Given.ToLetterOrDigitName(section));
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
    private static string ToParameterIdentifier(string name, HashSet<string> usedIdentifiers) {
        string baseIdentifier = Given.ToLetterOrDigitName(name);
        if (baseIdentifier.Length == 0 || baseIdentifier == "_")
            baseIdentifier = "p";

        baseIdentifier = char.ToLowerInvariant(baseIdentifier[0]) + baseIdentifier.Substring(1);
        if (SyntaxFacts.GetKeywordKind(baseIdentifier) != SyntaxKind.None
            || SyntaxFacts.GetContextualKeywordKind(baseIdentifier) != SyntaxKind.None)
            baseIdentifier += "_";

        string candidate = baseIdentifier;
        var index = 2;
        while (!usedIdentifiers.Add(candidate)) {
            candidate = baseIdentifier + "_" + index;
            index++;
        }

        return candidate;
    }

    private static bool IsSupportedQueryType(string typeName) {
        string normalizedType = TrimNullable(typeName);
        if (IsSupportedQueryScalarType(normalizedType))
            return true;

        return TryGetCollectionElementType(normalizedType, out string elementType)
            && IsSupportedQueryScalarType(TrimNullable(elementType));
    }

    private static string TrimNullable(string typeName)
        => typeName.EndsWith("?", StringComparison.Ordinal) ? typeName.Substring(0, typeName.Length - 1) : typeName;

    private static bool IsSupportedQueryScalarType(string typeName)
        => typeName == "string" || typeName == "int" || typeName == "long" || typeName == "bool" || typeName == "double";

    private static bool TryGetCollectionElementType(string typeName, out string elementType) {
        const string prefix = "System.Collections.Generic.IReadOnlyList<";
        if (typeName.StartsWith(prefix, StringComparison.Ordinal) && typeName.EndsWith(">", StringComparison.Ordinal)) {
            elementType = typeName.Substring(prefix.Length, typeName.Length - prefix.Length - 1);
            return true;
        }

        elementType = string.Empty;
        return false;
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

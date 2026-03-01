namespace Geren.Map;

internal static class PathParametersAgainstPath {
    internal static bool Validate(string rawPath,
        string normalizedPath,
        ImmutableArray<ParamSpec> pathParams,
        ImmutableArray<Diagnostic>.Builder _diagnostics) {
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

        _diagnostics.Add(Diagnostic.Create(Givenn.PathParameterNameMismatch, Location.None, rawPath, string.Join("; ", details)));
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

            var name = path.Substring(open + 1, close - open - 1);
            if (name.Length > 0 && seen.Add(name))
                names.Add(name);

            i = close + 1;
        }

        return names;
    }
}

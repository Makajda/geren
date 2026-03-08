namespace Geren.Emit;

internal static class EmitUrl {
    internal static string EmitHelpers() => $$"""
    private static string BuildRequestUri(string path, Action<List<string>>? configureQuery = null)
    {
        if (configureQuery is null)
            return path;

        var query = new List<string>();
        configureQuery(query);
        return query.Count == 0 ? path : path + "?" + string.Join("&", query);
    }

    private static void AddQueryParameter(List<string> query, string name, object? value)
    {
        if (value is null)
            return;

        if (value is IEnumerable values && value is not string)
        {
            foreach (var item in values)
                AddQueryParameter(query, name, item);

            return;
        }

        string text = value switch
        {
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

        query.Add(name + "=" + Uri.EscapeDataString(text));
    }

    private static string FormatPathParameter(object? value)
    {
        string text = value switch
        {
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

        return Uri.EscapeDataString(text);
    }
""";

    internal static string BuildPathExpression(EndpointSpec endpoint) {
        string interpolatedPath = endpoint.Path;
        foreach (var param in endpoint.Params)
            interpolatedPath = interpolatedPath.Replace("{" + param.Name + "}", "{FormatPathParameter(" + param.Identifier + ")}");

        string pathExpr = $"$\"{interpolatedPath}\"";
        if (endpoint.Queries.Length == 0)
            return pathExpr;

        string queryBuilder = string.Join(Givenn.NewLine, endpoint.Queries.Select(static p =>
            $"            AddQueryParameter(query, \"{p.Name}\", {p.Identifier});"));

        return $$"""
        BuildRequestUri({{pathExpr}}, query =>
        {
{{queryBuilder}}
        })
""";
    }
}

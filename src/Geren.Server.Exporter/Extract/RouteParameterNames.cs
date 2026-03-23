namespace Geren.Server.Exporter.Extract;

internal static class RouteParameterNames {
    internal static HashSet<string> Extract(string routeTemplate) {
        HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);
        if (routeTemplate.Length == 0)
            return result;

        for (var i = 0; i < routeTemplate.Length; i++) {
            if (routeTemplate[i] != '{')
                continue;

            var end = routeTemplate.IndexOf('}', i + 1);
            if (end < 0)
                break;

            string inner = routeTemplate.Substring(i + 1, end - i - 1);
            string name = NormalizeRouteParameterInnerText(inner);
            if (name.Length != 0)
                result.Add(name);

            i = end;
        }

        return result;
    }

    private static string NormalizeRouteParameterInnerText(string inner) {
        string trimmed = inner.Trim();
        if (trimmed.Length == 0)
            return string.Empty;

        int start = 0;
        while (start < trimmed.Length && trimmed[start] == '*')
            start++;

        trimmed = trimmed[start..];

        int cutIndex = trimmed.Length;
        int colonIndex = trimmed.IndexOf(':');
        if (colonIndex >= 0)
            cutIndex = Math.Min(cutIndex, colonIndex);

        int questionIndex = trimmed.IndexOf('?');
        if (questionIndex >= 0)
            cutIndex = Math.Min(cutIndex, questionIndex);

        int equalsIndex = trimmed.IndexOf('=');
        if (equalsIndex >= 0)
            cutIndex = Math.Min(cutIndex, equalsIndex);

        return trimmed[..cutIndex].Trim();
    }
}

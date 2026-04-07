using System.Text.RegularExpressions;

namespace Geren.Server.Exporter.Common;

/// <summary>
/// Include/exclude filtering for extracted endpoints.
/// </summary>
internal sealed class EndpointFilters {
    private readonly ImmutableArray<Rule> _include;
    private readonly ImmutableArray<Rule> _exclude;

    private EndpointFilters(ImmutableArray<Rule> include, ImmutableArray<Rule> exclude) {
        _include = include;
        _exclude = exclude;
    }

    internal static bool TryCreate(string[]? include, string[]? exclude, out EndpointFilters filters, out string? error) {
        try {
            var includeRules = ParseRules(include);
            var excludeRules = ParseRules(exclude);
            filters = new EndpointFilters(includeRules, excludeRules);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or RegexParseException) {
            filters = new EndpointFilters([], []);
            error = ex.Message;
            return false;
        }
    }

    internal bool ShouldInclude(string path, string handlerNamespace) {
        // Include list acts as an allow-list when present.
        if (!_include.IsDefaultOrEmpty) {
            bool anyInclude = false;
            foreach (var rule in _include) {
                if (rule.IsMatch(path, handlerNamespace)) {
                    anyInclude = true;
                    break;
                }
            }

            if (!anyInclude)
                return false;
        }

        // Exclude list always wins.
        if (!_exclude.IsDefaultOrEmpty) {
            foreach (var rule in _exclude) {
                if (rule.IsMatch(path, handlerNamespace))
                    return false;
            }
        }

        return true;
    }

    private static ImmutableArray<Rule> ParseRules(string[]? inputs) {
        if (inputs is null || inputs.Length == 0)
            return ImmutableArray<Rule>.Empty;

        var builder = ImmutableArray.CreateBuilder<Rule>();
        foreach (var raw in inputs) {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            string trimmed = raw.Trim();
            var (field, pattern) = ParseField(trimmed);
            if (string.IsNullOrWhiteSpace(pattern))
                continue;

            var (kind, value) = ParseKind(pattern);
            if (field == Field.Path && kind == MatchKind.Glob)
                value = NormalizePathGlob(value);

            Regex regex = kind == MatchKind.Regex
                ? new(value, RegexOptions.CultureInvariant)
                : new(GlobToRegex(value), RegexOptions.CultureInvariant);

            builder.Add(new Rule(field, regex));
        }

        return builder.ToImmutable();
    }

    private static (Field Field, string Pattern) ParseField(string input) {
        // Accept "path:...", "route=...", "namespace:...", "ns=...".
        // If no known prefix is present, default to path/route.
        int sep = input.IndexOfAny([':', '=']);
        if (sep <= 0)
            return (Field.Path, input);

        string prefix = input[..sep].Trim();
        string rest = input[(sep + 1)..].Trim();

        if (prefix.Equals("path", StringComparison.OrdinalIgnoreCase) || prefix.Equals("route", StringComparison.OrdinalIgnoreCase))
            return (Field.Path, rest);

        if (prefix.Equals("namespace", StringComparison.OrdinalIgnoreCase) || prefix.Equals("ns", StringComparison.OrdinalIgnoreCase))
            return (Field.Namespace, rest);

        return (Field.Path, input);
    }

    private static (MatchKind Kind, string Value) ParseKind(string pattern) {
        if (pattern.StartsWith("re:", StringComparison.OrdinalIgnoreCase))
            return (MatchKind.Regex, pattern[3..]);

        if (pattern.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
            return (MatchKind.Regex, pattern[6..]);

        if (pattern.StartsWith("glob:", StringComparison.OrdinalIgnoreCase))
            return (MatchKind.Glob, pattern[5..]);

        return (MatchKind.Glob, pattern);
    }

    private static string NormalizePathGlob(string glob) {
        // Extracted route templates are normalized to start with "/".
        // For convenience, also treat "stat/*" as "/stat/*".
        if (string.IsNullOrWhiteSpace(glob))
            return glob;

        glob = glob.Trim();
        if (glob.StartsWith('/') || glob.StartsWith('*') || glob.StartsWith('?'))
            return glob;

        return "/" + glob;
    }

    private static string GlobToRegex(string glob) {
        // Full-string match. For substring matches, use "*foo*".
        string escaped = Regex.Escape(glob);
        escaped = escaped.Replace("\\*", ".*", StringComparison.Ordinal);
        escaped = escaped.Replace("\\?", ".", StringComparison.Ordinal);
        return "^" + escaped + "$";
    }

    private readonly record struct Rule(Field Field, Regex Regex) {
        internal bool IsMatch(string path, string handlerNamespace) =>
            Field switch {
                Field.Path => Regex.IsMatch(path),
                Field.Namespace => Regex.IsMatch(handlerNamespace),
                _ => false,
            };
    }

    private enum Field {
        Path,
        Namespace,
    }

    private enum MatchKind {
        Glob,
        Regex,
    }
}


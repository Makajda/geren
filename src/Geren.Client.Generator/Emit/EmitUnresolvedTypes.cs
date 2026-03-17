namespace Geren.Client.Generator.Emit;

internal static class EmitUnresolvedTypes {
    internal static string Run(string spaceName, IEnumerable<UnresolvedSchemaType> unresolved) {
        var items = unresolved
            .GroupBy(static x => x.PlaceholderTypeName, StringComparer.Ordinal)
            .Select(static g => Merge(g))
            .OrderBy(static x => x.PlaceholderTypeName, StringComparer.Ordinal)
            .ToArray();

        if (items.Length == 0)
            return string.Empty;

        return $$"""
#nullable enable
namespace {{spaceName}};

// This file is a support artifact:
// - Each type below is a placeholder emitted by the generator when it couldn't resolve a schema reference to a real CLR type.
// - Generated client code uses these placeholders to avoid producing extra compiler errors.
// - Share this file with the DTO/support team to fix missing or ambiguous types at the source.

{{string.Join(Givencg.NewLine + Givencg.NewLine, items.Select(EmitOne))}}
""";
    }

    private static UnresolvedSchemaType Merge(IGrouping<string, UnresolvedSchemaType> group) {
        var first = group.First();
        var details = group
            .Select(static x => x.Details)
            .Where(static d => !string.IsNullOrWhiteSpace(d))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return details.Length switch {
            0 => first with { Details = null },
            1 => first with { Details = details[0] },
            _ => first with { Details = string.Join("; ", details) }
        };
    }

    private static string EmitOne(UnresolvedSchemaType type) {
        var lines = new List<string>(8) {
                "// kind: " + Sanitize(type.Kind),
                "// requested: " + Sanitize(type.Requested),
            };

        if (!string.IsNullOrWhiteSpace(type.Details)) {
            foreach (var detailLine in SplitLines(type.Details!))
                lines.Add("// details: " + Sanitize(detailLine));
        }

        return string.Join(Givencg.NewLine, lines) + Givencg.NewLine
            + "internal sealed partial class " + type.PlaceholderTypeName + " { }";
    }

    private static IEnumerable<string> SplitLines(string value) {
        // Support both \n and \r\n, avoid emitting multiline comments unprefixed.
        var normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
        return normalized.Split(['\n'], StringSplitOptions.None);
    }

    private static string Sanitize(string value) => value.Replace("\t", " ").TrimEnd();
}

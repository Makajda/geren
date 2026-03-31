namespace Geren.Server.Exporter.Common;

internal sealed record ErLocation(string File, int Line, int Column);
internal sealed record ErWarning(string Id, string Message, ErLocation? Location = null);

internal static class Dide {
    internal const string UnableIEndpointRouteBuilder = "GERENEXP001";
    internal const string SkipTemplate = "GERENEXP002";
    internal const string SkipHandler = "GERENEXP003";
    internal const string SkipMethod = "GERENEXP004";

    internal static ErWarning Create(string id, string message) => new(id, message);

    internal static ErWarning Create(InvocationExpressionSyntax invocation, string id, string message) {
        FileLinePositionSpan span = invocation.GetLocation().GetLineSpan();
        string file = span.Path.Length == 0 ? "<unknown>" : span.Path;
        int line = span.StartLinePosition.Line + 1;
        int col = span.StartLinePosition.Character + 1;
        return new(id, message, new ErLocation(file, line, col));
    }

    internal static string ToString(IEnumerable<ErWarning> warnings) =>
        string.Join('\n',
            warnings.Select(w => $"{w.Id}: {w.Message}{(w.Location is null
                ? null
                : $": {w.Location.File}({w.Location.Line},{w.Location.Column})")}"));
}

namespace Geren.Server.Exporter.Common;

internal static class Dide {
    internal sealed record Location(string File, int Line, int Column);
    internal sealed record Warning(string Id, string Message, Location? Location = null);

    internal const string UnableIEndpointRouteBuilder = "GERENEXP001";
    internal const string SkipTemplate = "GERENEXP002";
    internal const string SkipHandler = "GERENEXP003";
    internal const string SkipMethod = "GERENEXP004";

    internal static Warning Create(string id, string message) => new(id, message);

    internal static Warning Create(InvocationExpressionSyntax invocation, string id, string message) {
        FileLinePositionSpan span = invocation.GetLocation().GetLineSpan();
        string file = span.Path.Length == 0 ? "<unknown>" : span.Path;
        int line = span.StartLinePosition.Line + 1;
        int col = span.StartLinePosition.Character + 1;
        return new(id, message, new Location(file, line, col));
    }

    internal static void Show(IEnumerable<Warning> warnings) {
        foreach (var warning in warnings) {
            if (warning.Location is null)
                Console.Error.WriteLine($"{warning.Id}: {warning.Message}");
            else
                Console.Error.WriteLine($"{warning.Location.File}({warning.Location.Line},{warning.Location.Column}): {warning.Id}: {warning.Message}");
        }
    }
}

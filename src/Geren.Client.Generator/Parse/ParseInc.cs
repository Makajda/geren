namespace Geren.Client.Generator.Parse;

internal sealed record ParseInc(
    bool Success,
    string FilePath,
    ImmutableArray<Purpoint> Purpoints,
    ImmutableArray<Diagnostic> Diagnostics) {

    private static ParseInc Skip(Diagnostic diagnostic) => new(false, string.Empty, [], [diagnostic]);

    internal static ParseInc Empty => new(false, string.Empty, [], []);

    internal static ParseInc Parse(AdditionalText file, CancellationToken cancellationToken) {
        try {
            string? text = file.GetText(cancellationToken)?.ToString();
            if (string.IsNullOrWhiteSpace(text))
                return Skip(Diagnostic.Create(Dide.JsonReadError, Location.None, $"Invalid {file.Path}: File is empty."));

            using MemoryStream ms = new(Encoding.UTF8.GetBytes(Given.ArraysDisguise(text!)));
            var readResult = OpenApiDocument.Load(ms);
            var errors = readResult.Diagnostic?.Errors;
            if (errors is not null && errors.Any())
                return Skip(Diagnostic.Create(Dide.ParseError, Location.None,
                    $"OpenAPI errors in {file.Path}: {string.Join("; ", errors.Select(e => e.Message))}"));

            OpenApiDocument? document = readResult.Document;
            if (document is null)
                return Skip(Diagnostic.Create(Dide.ParseError, Location.None, $"OpenAPI reader returned null for {file.Path}"));

            return new ParseSession().BuildMap(file.Path, document, cancellationToken);
        }
        catch (Exception ex) {
            return Skip(Diagnostic.Create(Dide.ParseError, Location.None,
                $"OpenAPI parse exception in {file.Path}: {ex.Message}"));
        }
    }
}

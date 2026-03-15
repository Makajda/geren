namespace Geren.Client.Generator.Incs;

internal class ParseInc {
    internal OpenApiDocument? Document { get; }
    internal string? FilePath { get; }
    internal bool Success { get; }
    internal Diagnostic? Diagnostic { get; }

    private ParseInc(OpenApiDocument? document, string? filePath, Diagnostic? diagnostic) {
        Success = true;
        Document = document;
        FilePath = filePath;
        Diagnostic = diagnostic;
    }

    //static
    private static ParseInc Ok(OpenApiDocument doc, string filePath)
        => new(doc, filePath, null);
    private static ParseInc Diag(Diagnostic diagnostic) => new(null, null, diagnostic);

    internal static ParseInc Parse(string filePath, string text) {
        try {
            using MemoryStream ms = new(Encoding.UTF8.GetBytes(Givencg.ArraysDisguise(text)));
            var readResult = OpenApiDocument.Load(ms);
            var errors = readResult.Diagnostic?.Errors ?? [];
            var document = readResult.Document;

            if (errors.Count > 0)
                return Diag(Diagnostic.Create(Dide.ParseError, Location.None,
                    $"OpenAPI errors in {filePath}: {string.Join("; ", errors.Select(e => e.Message))}"));

            if (document is null)
                return Diag(Diagnostic.Create(Dide.ParseError, Location.None,
                    $"OpenAPI reader returned null for {filePath}"));

            return Ok(document, filePath);
        }
        catch (Exception ex) {
            return Diag(Diagnostic.Create(Dide.ParseError, Location.None,
                $"OpenAPI parse exception in {filePath}: {ex.Message}"));
        }
    }
}

namespace Geren.Client.Generator.Incs;

internal sealed record ParseInc(OpenApiDocument? Document = null, string? FilePath = null, bool Success = false, Diagnostic? Diagnostic = null) {
    private static ParseInc Ok(OpenApiDocument doc, string filePath) => new(doc, filePath, true);
    private static ParseInc Diag(Diagnostic diagnostic) => new(null, null, false, diagnostic);

    internal static ParseInc Parse(string filePath, string text) {
        try {
            using MemoryStream ms = new(Encoding.UTF8.GetBytes(Givencg.ArraysDisguise(text)));
            var readResult = OpenApiDocument.Load(ms);
            var errors = readResult.Diagnostic?.Errors;
            if (errors is not null && errors.Any())
                return Diag(Diagnostic.Create(Dide.ParseError, Location.None,
                    $"OpenAPI errors in {filePath}: {string.Join("; ", errors.Select(e => e.Message))}"));

            OpenApiDocument? document = readResult.Document;
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

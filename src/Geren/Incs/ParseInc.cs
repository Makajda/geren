using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace Geren.Incs;

internal class ParseInc {
    internal OpenApiDocument? Document { get; }
    internal string? FilePath { get; }
    internal Diagnostic? Diagnostic { get; }

    private ParseInc(OpenApiDocument? document, string? filePath, Diagnostic? diagnostic) {
        Document = document;
        FilePath = filePath;
        Diagnostic = diagnostic;
    }

    //static
    internal static ParseInc Ok(OpenApiDocument doc, string filePath)
        => new(doc, filePath, null);
    internal static ParseInc Fail(Diagnostic diagnostic) => new(null, null, diagnostic);

    internal static ParseInc Parse(string filePath, string text) {
        try {
            var reader = new OpenApiStringReader();
            var document = reader.Read(text, out var diagnostic);

            if (diagnostic.Errors.Count > 0)
                return Fail(Diagnostic.Create(Givenn.ParseError, Location.None,
                    $"OpenAPI errors in {filePath}: {string.Join("; ", diagnostic.Errors.Select(e => e.Message))}"));

            if (document is null)
                return Fail(Diagnostic.Create(Givenn.ParseError, Location.None,
                    $"OpenAPI reader returned null for {filePath}"));

            return Ok(document, filePath);
        }
        catch (Exception ex) {
            return Fail(Diagnostic.Create(Givenn.ParseError, Location.None,
                $"OpenAPI parse exception in {filePath}: {ex.Message}"));
        }
    }
}

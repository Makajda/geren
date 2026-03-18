namespace Geren.Client.Generator.Parse;

internal sealed record ParseInc(
    bool Success,
    string FilePath,
    ImmutableArray<Purpoint> Purpoints,
    ImmutableArray<Diagnostic> Diagnostics) {
    internal static ParseInc Parse(AdditionalText file, CancellationToken cancellationToken)
        => new ParseSession().BuildMap(file, cancellationToken);
}

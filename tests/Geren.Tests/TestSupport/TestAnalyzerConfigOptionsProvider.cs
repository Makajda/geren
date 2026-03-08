namespace Geren.Tests.TestSupport;

internal sealed class TestAnalyzerConfigOptionsProvider(IDictionary<string, string>? globalOptions = null) : AnalyzerConfigOptionsProvider {
    private static readonly AnalyzerConfigOptions EmptyOptions = new DictionaryAnalyzerConfigOptions(null);
    private readonly AnalyzerConfigOptions _global = new DictionaryAnalyzerConfigOptions(globalOptions);

    public override AnalyzerConfigOptions GlobalOptions => _global;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => EmptyOptions;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => EmptyOptions;

    private sealed class DictionaryAnalyzerConfigOptions(IDictionary<string, string>? values) : AnalyzerConfigOptions {
        private readonly IReadOnlyDictionary<string, string> _values =
            values is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(values, StringComparer.Ordinal);

        public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);
    }
}

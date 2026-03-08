using System.Globalization;

namespace Geren.Tests.TestSupport;

internal sealed class CultureScope : IDisposable {
    private readonly CultureInfo _previousCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _previousUiCulture = CultureInfo.CurrentUICulture;

    internal CultureScope(string cultureName) {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public void Dispose() {
        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUiCulture;
    }
}

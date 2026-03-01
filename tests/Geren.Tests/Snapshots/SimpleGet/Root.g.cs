using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gereb.Generated.V1;

public sealed class Root
{
    private readonly HttpClient _http;
    public Root(HttpClient http) => _http = http;

    public async Task<string> GetOrders(int id, bool include, string search, CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync(new Func<string>(() =>
    {
        var query = new List<string>();
        query.Add("include=" + Uri.EscapeDataString(include ? "true" : "false"));
        query.Add("search=" + Uri.EscapeDataString(Convert.ToString(search, CultureInfo.InvariantCulture) ?? string.Empty));
        return $"/orders/{Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture) ?? string.Empty)}?{string.Join("&", query)}";
    })(), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}

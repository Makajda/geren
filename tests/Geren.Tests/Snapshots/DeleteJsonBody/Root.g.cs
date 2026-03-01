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

    public async Task<string> DeleteItems(int id, object body, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/items/{Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture) ?? string.Empty)}")
        {
            Content = JsonContent.Create(body)
        };
        var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}

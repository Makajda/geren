using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Demo.Generated.V1;

public sealed class Root
{
    private readonly HttpClient _http;
    public Root(HttpClient http) => _http = http;

    public async Task GetStatus(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync($"/status", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

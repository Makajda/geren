using System.Net;
using System.Net.Http;

namespace Geren.Tests.TestSupport;

internal sealed class RuntimeRecordingHandler : HttpMessageHandler {
    internal Uri? LastRequestUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        LastRequestUri = request.RequestUri;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("ok")
        });
    }
}

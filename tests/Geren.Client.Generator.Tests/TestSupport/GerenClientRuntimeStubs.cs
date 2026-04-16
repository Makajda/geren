using System.Net.Http;

namespace Geren;

// Test-only stub to satisfy generated code references.
// In real usage, this interface is provided by the Geren.Client runtime package.
public interface IGerenClientRequestHooks
{
    void PrepareRequest(HttpRequestMessage request);
}


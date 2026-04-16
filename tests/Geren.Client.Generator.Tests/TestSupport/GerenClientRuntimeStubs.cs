using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Geren;

// Test-only stub to satisfy generated code references.
// In real usage, this interface is provided by the Geren.Client runtime package.
public interface IGerenClientRequestHooks
{
    void PrepareRequest(HttpRequestMessage request);
}

// Test-only stub to satisfy generated code references.
// In real usage, this interface is provided by the Geren.Client runtime package.
public interface IGerenClientRequestHooksAsync
{
    ValueTask PrepareRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken);
}

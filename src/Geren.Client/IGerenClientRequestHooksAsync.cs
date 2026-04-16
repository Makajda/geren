using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Geren;

/// <summary>
/// Optional per-request async hook for Geren-generated API clients.
/// </summary>
/// <remarks>
/// This hook is intended for scenarios where request preparation requires awaiting asynchronous operations
/// (for example, fetching or refreshing an access token in Blazor WebAssembly).
/// <para/>
/// If you don't need async work, prefer <see cref="IGerenClientRequestHooks"/> to keep request preparation simple.
/// </remarks>
public interface IGerenClientRequestHooksAsync
{
    /// <summary>
    /// Mutates the outgoing <see cref="HttpRequestMessage"/> before sending.
    /// </summary>
    /// <param name="request">Outgoing request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PrepareRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken);
}


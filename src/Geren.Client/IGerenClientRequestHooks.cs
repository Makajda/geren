using System.Net.Http;

namespace Geren;

/// <summary>
/// Optional per-request hook for Geren-generated API clients.
/// </summary>
/// <remarks>
/// This is primarily useful for auth (for example, adding a bearer token) and other cross-cutting headers.
/// <para/>
/// In Blazor Server/SSR scenarios, this allows you to use scoped services (circuit scope) safely without relying on
/// <c>IHttpContextAccessor</c>, which may be <see langword="null"/> outside the initial HTTP request.
/// </remarks>
public interface IGerenClientRequestHooks
{
    /// <summary>
    /// Mutates the outgoing <see cref="HttpRequestMessage"/> before sending.
    /// </summary>
    /// <param name="request">Outgoing request.</param>
    void PrepareRequest(HttpRequestMessage request);
}

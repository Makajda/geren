namespace Geren.Client.Generator.Tests.Unit.Emit;

public sealed class EmitFactoryBridgeTests {
    [Fact]
    public void Run_EmitsHelpersUsedByClients() {
        var code = EmitFactoryBridge.Run("Acme");

        code.Should().Contain("public static class FactoryBridge");
        code.Should().Contain("public static void AddClient<TClient>(");
        code.Should().Contain("public abstract partial class GerenClientBase");
        code.Should().Contain("protected GerenClientBase(HttpClient http, IServiceProvider services)");
        code.Should().Contain("services.GetService<global::Geren.IGerenClientRequestHooks>()");
        code.Should().Contain("services.GetService<global::Geren.IGerenClientRequestHooksAsync>()");
        code.Should().Contain("protected ValueTask PrepareRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)");
        code.Should().Contain("protected static string BuildRequestUri");
        code.Should().Contain("protected static void AddQueryParameter(");
        code.Should().Contain("protected static string FormatPathValue(");

        // PrepareRequest: sync hook should run before the static partial hook.
        var hooksIndex = code.IndexOf("_requestHooks?.PrepareRequest(request);", StringComparison.Ordinal);
        var partialIndex = code.IndexOf("OnPrepareRequest(request);", StringComparison.Ordinal);
        hooksIndex.Should().BeGreaterThan(0);
        partialIndex.Should().BeGreaterThan(0);
        hooksIndex.Should().BeLessThan(partialIndex);

        // PrepareRequestAsync: async hook should run before PrepareRequest(request).
        var asyncStart = code.IndexOf("protected ValueTask PrepareRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)", StringComparison.Ordinal);
        asyncStart.Should().BeGreaterThan(0);
        var asyncEnd = code.IndexOf("static partial void OnPrepareRequest", asyncStart, StringComparison.Ordinal);
        asyncEnd.Should().BeGreaterThan(asyncStart);

        var asyncBody = code.Substring(asyncStart, asyncEnd - asyncStart);
        var asyncHooksIndex = asyncBody.IndexOf("await _requestHooksAsync.PrepareRequestAsync(request, cancellationToken)", StringComparison.Ordinal);
        asyncHooksIndex.Should().BeGreaterThan(0);

        // There is also a no-async fast-path branch that calls PrepareRequest before returning.
        // We only care that in the async path, PrepareRequest runs after awaiting the async hook.
        var prepareAfterAwaitIndex = asyncBody.IndexOf("PrepareRequest(request);", asyncHooksIndex, StringComparison.Ordinal);
        prepareAfterAwaitIndex.Should().BeGreaterThan(asyncHooksIndex);
    }
}

namespace Geren.Tests.Emit;

public sealed class EmitFactoryBridgeTests {
    [Fact]
    public void Run_should_emit_resilience_branch_when_enabled() {
        var code = EmitFactoryBridge.Run(rootNamespace: "Company.Generated");

        code.Should().Contain("using Microsoft.Extensions.Http.Resilience;");
        code.Should().Contain("if (useResilience ?? false)");
        code.Should().Contain("builder.AddStandardResilienceHandler();");
        code.Should().Contain("configureBuilder?.Invoke(builder);");
    }

    [Fact]
    public void Run_should_emit_query_builder_for_bool_and_numeric_values() {
        var code = EmitFactoryBridge.Run(rootNamespace: "Company.Generated");

        code.Should().Contain("internal static string BuildRequestUri(string path, Action<List<string>>? configureQuery = null)");
        code.Should().Contain("internal static void A(List<string> query, string name, object? value)");
        code.Should().Contain("internal static string V(object? value)=> Uri.EscapeDataString(");
    }
}

namespace Geren.Client.Generator.Tests.Unit.Emit;

public sealed class EmitUnresolvedTypesTests {
    [Fact]
    public void Run_EmitsSupportFileWithMergedDetails() {
        var types = new[] {
            new UnresolvedSchemaType("__GerenUnresolvedType_ABCDEF123456", "metadata", "Dto.Missing", "referenceId: Missing"),
            new UnresolvedSchemaType("__GerenUnresolvedType_ABCDEF123456", "metadata", "Dto.Missing", "other: info")
        };

        var code = EmitUnresolvedTypes.Run("Acme.Petstore", types);

        code.Should().Contain("namespace Acme.Petstore;");
        code.Should().Contain("public sealed partial class __GerenUnresolvedType_ABCDEF123456 { }");
        code.Should().Contain("// kind: metadata");
        code.Should().Contain("// requested: Dto.Missing");
        code.Should().Contain("// details: referenceId: Missing; other: info");
    }
}


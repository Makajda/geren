namespace Geren.Client.Generator.Tests.Unit.Map;

public sealed class TypeResolverTests {
    [Fact]
    public void Resolve_Metadata_Succeeds_WhenTypeExistsByMetadataName() {
        var compilation = CompilationFactory.Create("t", """
            namespace Dto;
            public sealed class PetDto { }
            """);

        var diags = ImmutableArray.CreateBuilder<Diagnostic>();
        var unresolved = new Dictionary<string, UnresolvedSchemaType>(StringComparer.Ordinal);
        var resolver = new TypeResolver("Acme.Spec", compilation, unresolved, diags, CancellationToken.None);

        var resolved = resolver.Resolve(new PurposeType("Dto.PetDto", Puresolve.Metadata));

        resolved.Should().Be("global::Dto.PetDto");
        diags.Should().BeEmpty();
        unresolved.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_Metadata_Missing_UsesPlaceholderAndReportsWarningOnce() {
        var compilation = CompilationFactory.Create("t", "public sealed class Dummy { }");
        var diags = ImmutableArray.CreateBuilder<Diagnostic>();
        var unresolved = new Dictionary<string, UnresolvedSchemaType>(StringComparer.Ordinal);
        var resolver = new TypeResolver("Acme.Spec", compilation, unresolved, diags, CancellationToken.None);

        var r1 = resolver.Resolve(new PurposeType("Dto.Missing", Puresolve.Metadata));
        var r2 = resolver.Resolve(new PurposeType("Dto.Missing", Puresolve.Metadata));

        r1.Should().StartWith("global::Acme.Spec.__GerenUnresolvedType_");
        r2.Should().Be(r1, "type names are cached");
        diags.Should().ContainSingle(d => d.Id == "GEREN007");
        unresolved.Values.Should().ContainSingle(u => u.Kind == "metadata" && u.Requested == "Dto.Missing");
    }

    [Fact]
    public void Resolve_Reference_Succeeds_WhenUnique() {
        var compilation = CompilationFactory.Create("t", """
            namespace A;
            public sealed class PetDto { }
            """);

        var diags = ImmutableArray.CreateBuilder<Diagnostic>();
        var unresolved = new Dictionary<string, UnresolvedSchemaType>(StringComparer.Ordinal);
        var resolver = new TypeResolver("Acme.Spec", compilation, unresolved, diags, CancellationToken.None);

        var resolved = resolver.Resolve(new PurposeType("PetDto", Puresolve.Reference));

        resolved.Should().Be("global::A.PetDto");
        diags.Should().BeEmpty();
        unresolved.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_Reference_Missing_ReportsWarningAndUsesPlaceholder() {
        var compilation = CompilationFactory.Create("t", "public sealed class Dummy { }");
        var diags = ImmutableArray.CreateBuilder<Diagnostic>();
        var unresolved = new Dictionary<string, UnresolvedSchemaType>(StringComparer.Ordinal);
        var resolver = new TypeResolver("Acme.Spec", compilation, unresolved, diags, CancellationToken.None);

        var resolved = resolver.Resolve(new PurposeType("MissingDto", Puresolve.Reference));

        resolved.Should().StartWith("global::Acme.Spec.__GerenUnresolvedType_");
        diags.Should().ContainSingle(d => d.Id == "GEREN007");
        unresolved.Values.Should().ContainSingle(u => u.Kind == "reference" && u.Requested == "MissingDto");
    }

    [Fact]
    public void Resolve_Reference_Ambiguous_ReportsDiagnosticAndUsesPlaceholder() {
        var compilation = CompilationFactory.Create("t", """
            namespace A;
            public sealed class PetDto { }

            namespace B;
            public sealed class PetDto { }
            """);

        var diags = ImmutableArray.CreateBuilder<Diagnostic>();
        var unresolved = new Dictionary<string, UnresolvedSchemaType>(StringComparer.Ordinal);
        var resolver = new TypeResolver("Acme.Spec", compilation, unresolved, diags, CancellationToken.None);

        var resolved = resolver.Resolve(new PurposeType("PetDto", Puresolve.Reference));

        resolved.Should().StartWith("global::Acme.Spec.__GerenUnresolvedType_");
        diags.Should().ContainSingle(d => d.Id == "GEREN014");
        unresolved.Values.Should().ContainSingle(u => u.Kind == "ambiguous" && u.Requested == "PetDto");
    }

    [Fact]
    public void Resolve_Reference_Ambiguous_FormatsPreviewWhenManyMatches() {
        var compilation = CompilationFactory.Create("t", """
            namespace A0 { public sealed class PetDto { } }
            namespace A1 { public sealed class PetDto { } }
            namespace A2 { public sealed class PetDto { } }
            namespace A3 { public sealed class PetDto { } }
            namespace A4 { public sealed class PetDto { } }
            namespace A5 { public sealed class PetDto { } }
            namespace A6 { public sealed class PetDto { } }
            """);

        var diags = ImmutableArray.CreateBuilder<Diagnostic>();
        var unresolved = new Dictionary<string, UnresolvedSchemaType>(StringComparer.Ordinal);
        var resolver = new TypeResolver("Acme.Spec", compilation, unresolved, diags, CancellationToken.None);

        _ = resolver.Resolve(new PurposeType("PetDto", Puresolve.Reference));

        diags.Should().ContainSingle(d => d.Id == "GEREN014" && d.GetMessage().Contains("... (+", StringComparison.Ordinal));
        unresolved.Values.Single().Details.Should().Contain("... (+", "preview truncation keeps diagnostics readable");
    }

    [Fact]
    public void Resolve_Compile_InvalidType_UsesPlaceholderAndReportsWarning() {
        var compilation = CompilationFactory.Create("t", "public sealed class Dummy { }");
        var diags = ImmutableArray.CreateBuilder<Diagnostic>();
        var unresolved = new Dictionary<string, UnresolvedSchemaType>(StringComparer.Ordinal);
        var resolver = new TypeResolver("Acme.Spec", compilation, unresolved, diags, CancellationToken.None);

        var resolved = resolver.Resolve(new PurposeType("System.Collections.Generic.List<KeyValuePair<string, MissingType>>", Puresolve.Compile));

        resolved.Should().StartWith("global::Acme.Spec.__GerenUnresolvedType_");
        diags.Should().ContainSingle(d => d.Id == "GEREN007");
        unresolved.Values.Should().ContainSingle(u => u.Kind == "compile");
    }
}

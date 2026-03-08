namespace Geren.Server.Tests;

public sealed class TransformerTests {
    [Fact]
    public async Task TransformAsync_should_do_nothing_when_context_has_no_type() {
        var transformer = new Transformer();
        var schema = new OpenApiSchema();

        await transformer.TransformAsync(schema, OpenApiSchemaTransformerContextFactory.CreateWithoutType(), CancellationToken.None);

        schema.Extensions.Should().BeNull();
    }

    [Fact]
    public async Task TransformAsync_should_ignore_alias_types() {
        var transformer = new Transformer();
        var schema = new OpenApiSchema();

        await transformer.TransformAsync(schema, OpenApiSchemaTransformerContextFactory.Create(typeof(int)), CancellationToken.None);

        schema.Extensions.Should().BeNull();
    }

    [Fact]
    public async Task TransformAsync_should_emit_x_metadata_for_plain_types() {
        var transformer = new Transformer();
        var schema = new OpenApiSchema();

        await transformer.TransformAsync(schema, OpenApiSchemaTransformerContextFactory.Create(typeof(Widget)), CancellationToken.None);

        schema.Extensions.Should().ContainKey("x-metadata");
        GetExtensionValue(schema, "x-metadata").Should().Be("Geren.Server.Tests.TransformerTests+Widget");
    }

    [Fact]
    public async Task TransformAsync_should_emit_x_metadata_for_nested_types() {
        var transformer = new Transformer();
        var schema = new OpenApiSchema();

        await transformer.TransformAsync(schema, OpenApiSchemaTransformerContextFactory.Create(typeof(Outer.Inner)), CancellationToken.None);

        schema.Extensions.Should().ContainKey("x-metadata");
        GetExtensionValue(schema, "x-metadata").Should().Be("Geren.Server.Tests.TransformerTests+Outer+Inner");
    }

    [Fact]
    public async Task TransformAsync_should_emit_x_compile_for_generic_and_array_types() {
        var transformer = new Transformer();
        var genericSchema = new OpenApiSchema();
        var arraySchema = new OpenApiSchema();

        await transformer.TransformAsync(genericSchema, OpenApiSchemaTransformerContextFactory.Create(typeof(List<Widget>)), CancellationToken.None);
        await transformer.TransformAsync(arraySchema, OpenApiSchemaTransformerContextFactory.Create(typeof(Widget[])), CancellationToken.None);

        genericSchema.Extensions.Should().ContainKey("x-compile");
        GetExtensionValue(genericSchema, "x-compile")
            .Should()
            .Be("global::System.Collections.Generic.List<global::Geren.Server.Tests.TransformerTests.Widget>");

        arraySchema.Extensions.Should().ContainKey("x-compile");
        GetExtensionValue(arraySchema, "x-compile")
            .Should()
            .Be("global::Geren.Server.Tests.TransformerTests.Widget[]");
    }

    private static string GetExtensionValue(OpenApiSchema schema, string key) =>
        ((JsonNodeExtension)schema.Extensions![key]).Node.GetValue<string>();

    public sealed class Widget;

    public sealed class Outer {
        public sealed class Inner;
    }
}

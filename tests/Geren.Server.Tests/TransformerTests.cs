namespace Geren.Server.Tests;

public sealed class TransformerTests {
    [Fact]
    public void ApplySchemaExtensions_should_do_nothing_when_type_is_null() {
        var schema = new OpenApiSchema();

        Transformer.ApplySchemaExtensions(schema, null);

        schema.Extensions.Should().BeNull();
    }

    [Fact]
    public void ApplySchemaExtensions_should_ignore_alias_types() {
        var schema = new OpenApiSchema();

        Transformer.ApplySchemaExtensions(schema, typeof(int));

        schema.Extensions.Should().BeNull();
    }

    [Fact]
    public void ApplySchemaExtensions_should_emit_x_metadata_for_plain_types() {
        var schema = new OpenApiSchema();

        Transformer.ApplySchemaExtensions(schema, typeof(Widget));

        schema.Extensions.Should().ContainKey("x-metadata");
        GetExtensionValue(schema, "x-metadata").Should().Be("Geren.Server.Tests.TransformerTests+Widget");
    }

    [Fact]
    public void ApplySchemaExtensions_should_emit_x_metadata_for_nested_types() {
        var schema = new OpenApiSchema();

        Transformer.ApplySchemaExtensions(schema, typeof(Outer.Inner));

        schema.Extensions.Should().ContainKey("x-metadata");
        GetExtensionValue(schema, "x-metadata").Should().Be("Geren.Server.Tests.TransformerTests+Outer+Inner");
    }

    [Fact]
    public void ApplySchemaExtensions_should_emit_x_compile_for_generic_and_array_types() {
        var genericSchema = new OpenApiSchema();
        var arraySchema = new OpenApiSchema();

        Transformer.ApplySchemaExtensions(genericSchema, typeof(List<Widget>));
        Transformer.ApplySchemaExtensions(arraySchema, typeof(Widget[]));

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

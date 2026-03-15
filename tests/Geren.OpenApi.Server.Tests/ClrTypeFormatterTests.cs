namespace Geren.Server.Tests;

public sealed class ClrTypeFormatterTests {
    [Fact]
    public void Format_should_use_csharp_aliases_and_nullable_suffixes() {
        ClrTypeFormatter.Format(typeof(int)).Should().Be("int");
        ClrTypeFormatter.Format(typeof(int?)).Should().Be("int?");
        ClrTypeFormatter.Format(typeof(string)).Should().Be("string");
    }

    [Fact]
    public void Format_should_render_arrays_and_multidimensional_arrays() {
        ClrTypeFormatter.Format(typeof(string[])).Should().Be("string[]");
        ClrTypeFormatter.Format(typeof(int[,])).Should().Be("int[,]");
        ClrTypeFormatter.Format(typeof(decimal[,,])).Should().Be("decimal[,,]");
    }

    [Fact]
    public void Format_should_render_generic_and_nested_generic_types() {
        ClrTypeFormatter.Format(typeof(Dictionary<string, List<int?>>))
            .Should()
            .Be("global::System.Collections.Generic.Dictionary<string, global::System.Collections.Generic.List<int?>>");

        ClrTypeFormatter.Format(typeof(Container<int>.Nested<long>))
            .Should()
            .Be("global::Geren.Server.Tests.ClrTypeFormatterTests.Container<int>.Nested<long>");
    }

    [Fact]
    public void Format_should_render_value_tuples() {
        ClrTypeFormatter.Format(typeof((int Id, string Name))).Should().Be("(int, string)");
    }

    [Fact]
    public void Format_should_flatten_long_value_tuples() {
        ClrTypeFormatter.Format(typeof((int A, int B, int C, int D, int E, int F, int G, int H, int I)))
            .Should()
            .Be("(int, int, int, int, int, int, int, int, int)");
    }

    public sealed class Container<T> {
        public sealed class Nested<TInner>;
    }
}

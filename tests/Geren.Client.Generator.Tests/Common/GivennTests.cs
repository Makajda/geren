using Geren.Client.Generator.Common;

namespace Geren.Tests.Common;

public sealed class GivennTests {
    [Fact]
    public void ArraysDisguise_and_restore_should_round_trip_multidimensional_markers() {
        const string source = "global::Contracts.Matrix[,,,][] and global::Contracts.Cube[,,]";

        var disguised = Givencg.ArraysDisguise(source);
        var restored = Givencg.ArraysRestore(disguised);

        disguised.Should().NotContain("[");
        restored.Should().Be(source);
    }

    [Theory]
    [InlineData("", "_")]
    [InlineData("pet-name", "Pet_name")]
    [InlineData("9lives", "_9lives")]
    [InlineData("!!!", "___")]
    [InlineData("_already_valid", "_already_valid")]
    public void ToLetterOrDigitName_should_normalize_to_safe_identifier(string value, string expected) {
        Givencg.ToLetterOrDigitName(value).Should().Be(expected);
    }
}

namespace Geren.Client.Generator.Common;

internal static class Given {
    internal const string NewLine = "\n";

    internal const string Get = "Get";
    internal const string Post = "Post";
    internal const string Put = "Put";
    internal const string Patch = "Patch";
    internal const string Delete = "Delete";

    internal static string ArraysDisguise(string value) => value
        .Replace("[]", "--")
        .Replace("[,]", "-_-")
        .Replace("[,,]", "-__-")
        .Replace("[,,,]", "-___-")
        .Replace("[,,,,]", "-____-")
        .Replace("[,,,,,]", "-_____-")
        .Replace("[,,,,,,]", "-______-")
        .Replace("[,,,,,,,]", "-_______-");

    internal static string ArraysRestore(string value) => value
        .Replace("-_______-", "[,,,,,,,]")
        .Replace("-______-", "[,,,,,,]")
        .Replace("-_____-", "[,,,,,]")
        .Replace("-____-", "[,,,,]")
        .Replace("-___-", "[,,,]")
        .Replace("-__-", "[,,]")
        .Replace("-_-", "[,]")
        .Replace("--", "[]");

    internal static string ToLetterOrDigitName(string value) {
        if (value.Length == 0) return "_";
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
            sb.Append(char.IsLetterOrDigit(ch) ? ch : "_");

        var result = sb.ToString();
        if (result.Length == 0) return "_";
        if (result[0] == '_') return result;
        return char.IsLetter(result[0])
            ? char.ToUpperInvariant(result[0]) + result.Substring(1)
            : "_" + result;
    }
}

using System.Runtime.CompilerServices;
using System.Text;

namespace Geren.Client.Generator.Tests.TestSupport;

internal static class Snapshots {
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    internal static bool UpdateMode { get; } =
        string.Equals(Environment.GetEnvironmentVariable("GEREN_UPDATE_SNAPSHOTS"), "1", StringComparison.Ordinal) ||
        string.Equals(Environment.GetEnvironmentVariable("GEREN_UPDATE_SNAPSHOTS"), "true", StringComparison.OrdinalIgnoreCase);

    internal static void ShouldMatch(string snapshotName, string actual) {
        if (string.IsNullOrWhiteSpace(snapshotName))
            throw new ArgumentException("Snapshot name is required.", nameof(snapshotName));

        var snapshotPath = GetSnapshotPath(snapshotName);
        var normalizedActual = Normalize(actual);

        if (UpdateMode) {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            File.WriteAllText(snapshotPath, normalizedActual, Utf8NoBom);
            return;
        }

        if (!File.Exists(snapshotPath))
            throw new FileNotFoundException($"Snapshot not found: {snapshotPath}. Set GEREN_UPDATE_SNAPSHOTS=1 to create/update snapshots.");

        var expected = File.ReadAllText(snapshotPath, Utf8NoBom);
        var normalizedExpected = Normalize(expected);

        normalizedActual.Should().Be(normalizedExpected, $"snapshot must match '{snapshotPath}'");
    }

    private static string GetSnapshotPath(string snapshotName) {
        var repoRoot = FindRepoRoot();
        return Path.Combine(repoRoot, "tests", "Geren.Client.Generator.Tests", "Snapshots", snapshotName);
    }

    private static string FindRepoRoot() {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null) {
            if (File.Exists(Path.Combine(dir.FullName, "geren.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root (geren.slnx not found).");
    }

    private static string Normalize(string text) {
        if (text is null)
            return string.Empty;

        // Normalize EOL to '\n' and ensure trailing newline for stable diffs.
        var t = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        return t.EndsWith("\n", StringComparison.Ordinal) ? t : t + "\n";
    }
}

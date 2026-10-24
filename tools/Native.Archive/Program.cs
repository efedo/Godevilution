using System.Security.Cryptography;
using System.Text.Json;
using Godevilution.Tools.Archive;

const string Usage = """
    Native.Archive digest <archive> <name>...   JSON size/SHA-256 per name (absent names included)
    Native.Archive extract <archive> <name> <output-file>
    """;

try {
    switch (args) {
    case ["digest", var archivePath, .. var names] when names.Length > 0: {
        using var archive = MpqArchive.Open(archivePath);
        var files = names.Select(name => {
            if (!archive.Contains(name))
                return new { name, exists = false, size = 0L, sha256 = (string?)null };
            var data = archive.ReadFile(name);
            return new { name, exists = true, size = (long)data.Length, sha256 = (string?)Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant() };
        }).ToArray();
        Console.WriteLine(JsonSerializer.Serialize(new { archive = Path.GetFullPath(archivePath), files },
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    case ["extract", var archivePath, var name, var outputPath]: {
        using var archive = MpqArchive.Open(archivePath);
        var output = Path.GetFullPath(outputPath);
        if (File.Exists(output))
            throw new IOException($"Refusing to overwrite '{output}'.");
        File.WriteAllBytes(output, archive.ReadFile(name));
        Console.WriteLine($"Extracted {name} to {output}");
        return 0;
    }
    default:
        Console.Error.WriteLine(Usage);
        return 2;
    }
} catch (Exception failure) when (failure is IOException or ArgumentException) {
    Console.Error.WriteLine(failure.Message);
    return 1;
}

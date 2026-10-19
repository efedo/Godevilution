using System.Text;
using System.Text.Json;

namespace Devilution.Client.Acceptance;

/** Reads atomically published marker files with sharing that permits rename. */
public static class PerformanceMarkerFileReader
{
    public static T? ReadJson<T>(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096,
            options: FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return JsonSerializer.Deserialize<T>(reader.ReadToEnd());
    }
}

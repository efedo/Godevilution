using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Devilution.Client.Acceptance;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class PerformanceMarkerFileReaderTests
{
    [Fact]
    public void ReadsCompleteJsonWithOrdinaryFileAccess()
    {
        WithMarkerFile(Encoding.UTF8.GetBytes("{\"Passed\":true,\"Sequence\":17}"), path => {
            var marker = PerformanceMarkerFileReader.ReadJson<TestMarker>(path);

            Assert.Equal(new TestMarker(true, 17), marker);
        });
    }

    [Fact]
    public void RejectsMalformedJsonWithoutRetryingOrRepairingIt()
    {
        WithMarkerFile(Encoding.UTF8.GetBytes("{\"Passed\":true,\"Sequence\":17"), path => {
            Assert.Throws<JsonException>(() => PerformanceMarkerFileReader.ReadJson<TestMarker>(path));
        });
    }

    [Fact]
    public void MatchesLegacyReadAllTextForUtf8Bom()
    {
        var json = Encoding.UTF8.GetBytes("{\"Text\":\"marker\"}");
        var contents = Encoding.UTF8.GetPreamble().Concat(json).ToArray();
        WithMarkerFile(contents, path => {
            var legacy = JsonSerializer.Deserialize<TextMarker>(File.ReadAllText(path));
            var actual = PerformanceMarkerFileReader.ReadJson<TextMarker>(path);

            Assert.Equal(legacy, actual);
        });
    }

    [Fact]
    public void MatchesLegacyReadAllTextForInvalidUtf8InsideJsonString()
    {
        var prefix = Encoding.UTF8.GetBytes("{\"Text\":\"");
        var suffix = Encoding.UTF8.GetBytes("\"}");
        var contents = prefix.Concat(new byte[] { byte.MaxValue }).Concat(suffix).ToArray();
        WithMarkerFile(contents, path => {
            var legacy = JsonSerializer.Deserialize<TextMarker>(File.ReadAllText(path));
            var actual = PerformanceMarkerFileReader.ReadJson<TextMarker>(path);

            Assert.Equal(legacy, actual);
        });
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "The delete-sharing regression requires Windows file semantics.")]
    public void AllowsReadWhileADeleteAccessHandleIsOpenOnWindows()
    {
        WithMarkerFile(Encoding.UTF8.GetBytes("{\"Passed\":true,\"Sequence\":19}"), path => {
            using var deleteHandle = OpenDeleteAccessHandle(path);

            var legacyRead = Assert.Throws<IOException>(() => File.ReadAllText(path));
            Assert.Equal(unchecked((int)0x80070020), legacyRead.HResult);

            var marker = PerformanceMarkerFileReader.ReadJson<TestMarker>(path);
            Assert.Equal(new TestMarker(true, 19), marker);
        });
    }

    public static bool IsWindows => OperatingSystem.IsWindows();

    private static SafeFileHandle OpenDeleteAccessHandle(string path)
    {
        var handle = CreateFileW(
            path,
            DeleteAccess,
            ShareRead | ShareWrite | ShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileAttributeNormal,
            IntPtr.Zero);
        if (handle.IsInvalid) {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(error);
        }
        return handle;
    }

    private static void WithMarkerFile(byte[] contents, Action<string> action)
    {
        var path = Path.Combine(Path.GetTempPath(), $"performance-marker-{Guid.NewGuid():N}.json");
        var ownsFile = false;
        Exception? primaryFailure = null;
        try {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) {
                ownsFile = true;
                stream.Write(contents);
            }
            action(path);
        } catch (Exception exception) {
            primaryFailure = exception;
            throw;
        } finally {
            if (ownsFile) {
                try {
                    File.Delete(path);
                } catch (Exception) when (primaryFailure is not null) {
                }
            }
        }
    }

    private sealed record TestMarker(bool Passed, int Sequence);
    private sealed record TextMarker(string Text);

    private const uint DeleteAccess = 0x00010000;
    private const uint ShareRead = 0x00000001;
    private const uint ShareWrite = 0x00000002;
    private const uint ShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);
}

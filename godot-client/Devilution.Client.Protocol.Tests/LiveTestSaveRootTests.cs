using System.ComponentModel;
using System.Diagnostics;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class LiveTestSaveRootTests
{
    [Fact]
    public void RegisteredOwnedRootDeletesAndMissingNestedPathIsANoop()
    {
        WithOwnedFixture(fixture => {
            var missing = Path.Combine(fixture, "save", "missing");
            LiveTestSaveRoot.Register(missing).Delete();
            Assert.False(Directory.Exists(missing));
            Assert.True(Directory.Exists(fixture));

            var marker = Path.Combine(fixture, "marker.txt");
            File.WriteAllText(marker, "owned");
            LiveTestSaveRoot.Register(fixture).Delete();

            Assert.False(Directory.Exists(fixture));
        });
    }

    [Fact]
    public void RefusesBroadUnregisteredAndExistingFileTargetsWithoutMutation()
    {
        WithOwnedFixture(fixture => {
            Assert.Throws<InvalidOperationException>(() => LiveTestSaveRoot.Register(Path.GetTempPath()));

            WithUnregisteredFixture(unregistered => {
                var unregisteredMarker = Path.Combine(unregistered, "marker.txt");
                File.WriteAllText(unregisteredMarker, "preserve-unregistered");
                Assert.Throws<InvalidOperationException>(() => LiveTestSaveRoot.Register(unregistered));
                Assert.Equal("preserve-unregistered", File.ReadAllText(unregisteredMarker));
            });

            var fileTarget = Path.Combine(fixture, "existing-file");
            File.WriteAllText(fileTarget, "preserve-file");
            Assert.Throws<InvalidOperationException>(() => LiveTestSaveRoot.Register(fileTarget));
            Assert.Equal("preserve-file", File.ReadAllText(fileTarget));
        });
    }

    [Fact]
    public void EligibleNestedSaveDeletePreservesSiblingsAndFixtureParent()
    {
        WithOwnedFixture(fixture => {
            var save = Path.Combine(fixture, "save");
            Directory.CreateDirectory(save);
            var saveMarker = Path.Combine(save, "marker.txt");
            File.WriteAllText(saveMarker, "delete-me");
            var sibling = Path.Combine(fixture, "sibling.txt");
            File.WriteAllText(sibling, "preserve");
            var siblingDirectory = Path.Combine(fixture, "sibling-directory");
            Directory.CreateDirectory(siblingDirectory);
            File.WriteAllText(Path.Combine(siblingDirectory, "marker.txt"), "preserve-directory");

            LiveTestSaveRoot.Register(save).Delete();

            Assert.False(Directory.Exists(save));
            Assert.True(Directory.Exists(fixture));
            Assert.Equal("preserve", File.ReadAllText(sibling));
            Assert.Equal("preserve-directory", File.ReadAllText(
                Path.Combine(siblingDirectory, "marker.txt")));
        });
    }

    [Fact]
    public void RefusesReparseAncestorWithoutFollowingOrDeletingTarget()
    {
        WithOwnedFixture(fixture => {
            var target = Path.Combine(fixture, "ancestor-target");
            Directory.CreateDirectory(target);
            var marker = Path.Combine(target, "marker.txt");
            File.WriteAllText(marker, "preserve-target");
            var link = Path.Combine(fixture, "ancestor-link");
            CreateDirectoryLinkOrSkip(link, target);

            Assert.Throws<InvalidOperationException>(() => LiveTestSaveRoot.Register(
                Path.Combine(link, "save")));
            Assert.True(IsReparsePoint(link));
            Assert.Equal("preserve-target", File.ReadAllText(marker));
        });
    }

    [Fact]
    public void RefusesDanglingReparseAncestorWithoutMutatingLink()
    {
        WithOwnedFixture(fixture => {
            var missingTarget = Path.Combine(fixture, "missing-target");
            Directory.CreateDirectory(missingTarget);
            var link = Path.Combine(fixture, "dangling-link");
            CreateDirectoryLinkOrSkip(link, missingTarget);
            Directory.Delete(missingTarget);

            Assert.Throws<InvalidOperationException>(() => LiveTestSaveRoot.Register(
                Path.Combine(link, "save")));
            Assert.True(IsReparsePoint(link));
            Assert.False(Directory.Exists(missingTarget));
        });
    }

    [Fact]
    public void RefusesReparseChildWithoutDeletingChildOrSiblings()
    {
        WithOwnedFixture(fixture => {
            var save = Path.Combine(fixture, "save");
            Directory.CreateDirectory(save);
            var target = Path.Combine(fixture, "child-target");
            Directory.CreateDirectory(target);
            var targetMarker = Path.Combine(target, "marker.txt");
            File.WriteAllText(targetMarker, "preserve-target");
            var link = Path.Combine(save, "child-link");
            CreateDirectoryLinkOrSkip(link, target);
            var sibling = Path.Combine(save, "sibling.txt");
            File.WriteAllText(sibling, "preserve-sibling");

            var lease = LiveTestSaveRoot.Register(save);
            Assert.Throws<InvalidOperationException>(() => lease.Delete());

            Assert.True(IsReparsePoint(link));
            Assert.Equal("preserve-target", File.ReadAllText(targetMarker));
            Assert.Equal("preserve-sibling", File.ReadAllText(sibling));
            Assert.True(Directory.Exists(save));
        });
    }

    [Fact]
    public void RefusesRegisteredRootReplacedByFileWithoutMutation()
    {
        WithOwnedFixture(fixture => {
            var lease = LiveTestSaveRoot.Register(fixture);
            Directory.Delete(fixture);
            File.WriteAllText(fixture, "preserve-replaced-root");

            Assert.Throws<InvalidOperationException>(() => lease.Delete());
            Assert.Equal("preserve-replaced-root", File.ReadAllText(fixture));
        });
    }

    private static void WithOwnedFixture(Action<string> body)
    {
        var fixture = Path.Combine(Path.GetTempPath(), "Godevilution-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        RunWithOwnedPath(fixture, body);
    }

    private static void WithUnregisteredFixture(Action<string> body)
    {
        var fixture = Path.Combine(Path.GetTempPath(), "Unregistered-Godevilution-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        RunWithOwnedPath(fixture, body);
    }

    private static void RunWithOwnedPath(string fixture, Action<string> body)
    {
        Exception? primary = null;
        try {
            body(fixture);
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            try {
                DeleteFixtureTree(fixture);
            } catch (Exception cleanup) when (primary is not null) {
                primary.Data["FixtureCleanupFailure"] = cleanup;
            }
        }
    }

    private static void CreateDirectoryLinkOrSkip(string link, string target)
    {
        if (OperatingSystem.IsWindows()) {
            try {
                Directory.CreateSymbolicLink(link, target);
                return;
            } catch (UnauthorizedAccessException error) {
                TryCreateJunctionOrSkip(link, target, error);
                return;
            } catch (PlatformNotSupportedException error) {
                Assert.Skip("The fixture platform does not support directory links: " + error.Message);
                return;
            } catch (NotSupportedException error) {
                Assert.Skip("The fixture platform does not support directory links: " + error.Message);
                return;
            } catch (IOException error) when ((error.HResult & 0xffff) is 1314 or 50 or 1) {
                TryCreateJunctionOrSkip(link, target, error);
                return;
            }
        }

        try {
            Directory.CreateSymbolicLink(link, target);
        } catch (UnauthorizedAccessException error) {
            Assert.Skip("The owned fixture cannot create a directory link: " + error.Message);
        } catch (PlatformNotSupportedException error) {
            Assert.Skip("The fixture platform does not support directory links: " + error.Message);
        } catch (NotSupportedException error) {
            Assert.Skip("The fixture platform does not support directory links: " + error.Message);
        } catch (IOException error) when ((error.HResult & 0xffff) is 1314 or 50 or 1) {
            Assert.Skip("The fixture filesystem does not permit directory links: " + error.Message);
        }
    }

    private static void TryCreateJunctionOrSkip(string link, string target, Exception symbolicLinkFailure)
    {
        var start = new ProcessStartInfo("powershell.exe") {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add($"New-Item -ItemType Junction -Path '{EscapePowerShellLiteral(link)}' -Target '{EscapePowerShellLiteral(target)}' | Out-Null");
        using var process = StartJunctionHelperOrSkip(start, symbolicLinkFailure);
        if (!process.WaitForExit(10000)) {
            process.Kill();
            if (!process.WaitForExit(5000))
                throw new TimeoutException("The owned junction helper did not terminate after termination was requested.");
            throw new TimeoutException("The owned junction helper exceeded its bounded execution window.");
        }
        var errorOutput = process.StandardError.ReadToEnd();
        if (process.ExitCode != 0) {
            if (IsLinkPermissionFailure(errorOutput))
                Assert.Skip("The fixture filesystem does not permit directory links: "
                    + symbolicLinkFailure.Message + "; junction fallback: " + errorOutput);
            throw new InvalidOperationException("The owned junction helper failed: " + errorOutput);
        }
        if (!IsReparsePoint(link))
            throw new InvalidOperationException("The owned junction helper exited successfully without creating a reparse point.");
    }

    private static Process StartJunctionHelperOrSkip(ProcessStartInfo start, Exception symbolicLinkFailure)
    {
        try {
            return Process.Start(start)
                ?? throw new InvalidOperationException("Could not start the owned junction helper process.");
        } catch (Win32Exception error) {
            Assert.Skip("The fixture does not provide a junction helper process: "
                + symbolicLinkFailure.Message + "; " + error.Message);
            throw;
        }
    }

    private static bool IsLinkPermissionFailure(string errorOutput)
        => errorOutput.Contains("access denied", StringComparison.OrdinalIgnoreCase)
            || errorOutput.Contains("privilege", StringComparison.OrdinalIgnoreCase)
            || errorOutput.Contains("not supported", StringComparison.OrdinalIgnoreCase)
            || errorOutput.Contains("requires elevation", StringComparison.OrdinalIgnoreCase);

    private static string EscapePowerShellLiteral(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);

    private static bool IsReparsePoint(string path)
        => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static void DeleteFixtureTree(string path)
    {
        if (!TryGetAttributes(path, out var attributes))
            return;

        if ((attributes & FileAttributes.ReparsePoint) != 0) {
            DeleteReparseEntry(path, attributes);
            return;
        }

        if ((attributes & FileAttributes.Directory) == 0) {
            File.Delete(path);
            return;
        }

        foreach (var child in new DirectoryInfo(path).EnumerateFileSystemInfos())
            DeleteFixtureTree(child.FullName);
        Directory.Delete(path);
    }

    private static void DeleteReparseEntry(string path, FileAttributes attributes)
    {
        if ((attributes & FileAttributes.Directory) != 0)
            Directory.Delete(path);
        else
            File.Delete(path);
    }

    private static bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        try {
            attributes = File.GetAttributes(path);
            return true;
        } catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) {
            attributes = default;
            return false;
        }
    }
}

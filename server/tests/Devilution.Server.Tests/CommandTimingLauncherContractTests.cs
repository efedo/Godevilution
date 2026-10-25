using System.Text.RegularExpressions;
using Xunit;

namespace Devilution.Server.Tests;

/** Source contracts for opt-in diagnostics; actual timing requires application evidence. */
public sealed class CommandTimingLauncherContractTests
{
    [Theory]
    [InlineData("godot-client/run-local-client.ps1", "GodotExecutable,Port,CharacterClassId,CharacterName,Headless,QuitAfterSeconds,SessionDirectory,PersistenceMode,PreviewContent,PortalPreviewContent,LifecyclePreviewContent,Multiplayer,CathedralProfileDirectory,CathedralDepthProfileDirectory,CathedralDoorProfileDirectory,CathedralNativeProfileDirectory,PerformanceParticipants,AcceptancePhase", "CommandTimingTrace,Automated,PerformanceResumeBaselinePath,CathedralNativeProgressionProfileDirectory,ServerBuildConfiguration,CathedralNativeDepthProgressionProfileDirectory,NativeDepthEntryBaselinePath,SessionMenuAcceptance,CathedralNativeClassProfileDirectory,CathedralNativeTownProfileDirectory,TestParticipantPath")]
    [InlineData("tools/test-godot-session.ps1", "GodotExecutable,PersistenceMode,Visible,Journey,TownLoop,Lifecycle,Multiplayer,MultiplayerRecovery,MultiplayerPendingRecovery,CathedralProfileDirectory,CathedralDepthProfileDirectory,CathedralDoorProfileDirectory,CathedralNativeProfileDirectory,PresentationPackProfile,CommandTimingTrace,Cooperative", "CathedralNativeChest,CathedralNativeChestSessionDirectory,CathedralNativeSarcophagus,CathedralNativeSarcophagusSessionDirectory,CathedralNativeBarrel,CathedralNativeBarrelSessionDirectory,CathedralNativeTrap,CathedralNativeTrapSessionDirectory,SessionMenu")]
    public void TraceSwitchPreservesExistingPositionalParameterOrder(string path, string existingParameters, string appendedParameters)
    {
        var source = ReadSource(path);
        var parameterBlock = source[..source.IndexOf("$ErrorActionPreference", StringComparison.Ordinal)];
        var actual = Regex.Matches(parameterBlock, @"\$(\w+)")
            .Select(match => match.Groups[1].Value).ToArray();
        var expected = existingParameters.Split(',').Concat(appendedParameters.Split(',')).ToList();
        var optionalMenuParameter = path.StartsWith("godot-client/", StringComparison.Ordinal)
            ? "SessionMenuAcceptance" : "SessionMenu";
        if (actual.Contains(optionalMenuParameter, StringComparer.Ordinal)
            && !expected.Contains(optionalMenuParameter, StringComparer.Ordinal)) {
            var beforeParameter = path.StartsWith("godot-client/", StringComparison.Ordinal)
                ? "TrapDisarmPreviewContent" : "TrapDisarm";
            var insertionIndex = expected.FindIndex(parameter => string.Equals(parameter, beforeParameter, StringComparison.Ordinal));
            Assert.True(insertionIndex >= 0, $"Could not preserve the optional menu parameter position before {beforeParameter}.");
            expected.Insert(insertionIndex, optionalMenuParameter);
        }
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TimingCaptureIsExplicitAndRequiresAcceptance()
    {
        var launcher = ReadSource("godot-client/run-local-client.ps1");
        Assert.Contains("[switch]$CommandTimingTrace", launcher, StringComparison.Ordinal);
        Assert.Contains("if ($CommandTimingTrace -and [string]::IsNullOrWhiteSpace($AcceptancePhase))", launcher, StringComparison.Ordinal);
        Assert.Contains("Command timing traces require an explicit isolated acceptance phase.", launcher, StringComparison.Ordinal);
        Assert.Contains("$commandTimingArguments = if ($CommandTimingTrace) { '--command-timing-trace true' } else { '' }", launcher, StringComparison.Ordinal);
        Assert.Contains("--persistence-mode {5} {6}", launcher, StringComparison.Ordinal);
        Assert.Contains("$PersistenceMode, $commandTimingArguments", launcher, StringComparison.Ordinal);
        Assert.Contains("if ($CommandTimingTrace) { $clientEnvironment['GODEVILUTION_COMMAND_TIMING_TRACE'] = '1' }", launcher, StringComparison.Ordinal);
        Assert.Contains("if ($CommandTimingTrace -and $performance)", launcher, StringComparison.Ordinal);
        Assert.Contains("Command timing diagnostics cannot be combined with performance measurement.", launcher, StringComparison.Ordinal);
    }

    [Fact]
    public void SceneExportsOnFailureWithoutReplacingPrimaryException()
    {
        var scene = ReadSource("godot-client/Devilution.Client/Acceptance/SessionAcceptance.cs");
        var export = ReadSource("godot-client/Devilution.Client/Acceptance/SessionAcceptance.CommandTiming.cs");
        Assert.Contains("primaryFailure = exception;", scene, StringComparison.Ordinal);
        Assert.Contains("WriteCommandTimingEvidence(primaryFailure is null);", scene, StringComparison.Ordinal);
        Assert.Contains("if (primaryFailure is null)", scene, StringComparison.Ordinal);
        Assert.Contains("GetTree().Quit(primaryFailure is null ? 0 : 1);", scene, StringComparison.Ordinal);
        Assert.Contains("GetEnvironmentVariable(\"GODEVILUTION_COMMAND_TIMING_TRACE\") != \"1\"", export, StringComparison.Ordinal);
        Assert.DoesNotContain("SessionToken", export, StringComparison.Ordinal);
        Assert.DoesNotContain("ResumeToken", export, StringComparison.Ordinal);
    }

    [Fact]
    public void InheritedCaptureSettingsAreClearedAndRestored()
    {
        var launcher = ReadSource("godot-client/run-local-client.ps1");
        Assert.Contains("'GODEVILUTION_PERSISTENCE_MODE', 'GODEVILUTION_COMMAND_TIMING_TRACE'", launcher, StringComparison.Ordinal);
        Assert.Contains("foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $null, 'Process') }", launcher, StringComparison.Ordinal);
        Assert.Contains("foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }", launcher, StringComparison.Ordinal);
    }

    [Fact]
    public void DriverForwardsTraceOnlyWhenRequestedAndNormalLaunchersDoNotEnableIt()
    {
        var driver = ReadSource("tools/test-godot-session.ps1");
        Assert.Contains("[switch]$CommandTimingTrace", driver, StringComparison.Ordinal);
        Assert.Contains("if ($CommandTimingTrace) { $parameters.CommandTimingTrace = $true }", driver, StringComparison.Ordinal);
        foreach (var normal in new[] {
            "run-godot-client.ps1", "run-godot-preview.ps1", "run-godot-multiplayer.ps1",
        })
            Assert.DoesNotContain("CommandTimingTrace", ReadSource(normal), StringComparison.Ordinal);
    }

    [Fact]
    public void PerformanceHandlersShareTheExactCommandTimingOptInPredicate()
    {
        var create = ReadSource("godot-client/Devilution.Client/Acceptance/SessionAcceptance.Performance.cs");
        var resume = ReadSource("godot-client/Devilution.Client/Acceptance/SessionAcceptance.PerformanceResume.cs");
        var contract = ReadSource("godot-client/Devilution.Client/Acceptance/PerformanceResumeStateContract.cs");
        var timing = ReadSource("godot-client/Devilution.Client/Acceptance/SessionAcceptance.CommandTiming.cs");

        const string predicate = "PerformanceResumeStateContract.IsCommandTimingTraceEnabled(";
        Assert.Contains(predicate, create, StringComparison.Ordinal);
        Assert.Contains(predicate, resume, StringComparison.Ordinal);
        Assert.Contains(
            "string.Equals(value, \"1\", StringComparison.Ordinal)",
            contract,
            StringComparison.Ordinal);
        Assert.Contains(
            "GetEnvironmentVariable(\"GODEVILUTION_COMMAND_TIMING_TRACE\") != \"1\"",
            timing,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GetEnvironmentVariable(\"GODEVILUTION_COMMAND_TIMING_TRACE\") is null",
            create + resume,
            StringComparison.Ordinal);
    }
    private static string ReadSource(string relativePath)
    {
        var configured = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        foreach (var start in new[] { configured, Directory.GetCurrentDirectory(), AppContext.BaseDirectory }) {
            if (string.IsNullOrWhiteSpace(start))
                continue;
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
            }
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root.");
    }
}

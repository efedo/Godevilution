using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class LocalLauncherBehaviorTests
{
    [Fact]
    public void ClientConfigurationBuildsOptimizedImagesInTheProjectLaunchRuntimeSlot()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = BuildPowerShellHarness(["Get-GodotClientBuildArguments"], """
            foreach ($configuration in @('Debug', 'Release')) {
                $project = Join-Path ([IO.Path]::GetTempPath()) 'Godevilution client build path'
                $arguments = @(Get-GodotClientBuildArguments $project $configuration)
                $configIndex = [Array]::IndexOf($arguments, '--configuration')
                $outputIndex = [Array]::IndexOf($arguments, '--output')
                if ($configIndex -lt 0 -or $arguments[$configIndex + 1] -cne $configuration) { throw 'Client configuration was lost.' }
                if ($outputIndex -lt 0 -or $arguments[$outputIndex + 1] -cne (Join-Path $project '.godot/mono/temp/bin/Debug')) {
                    throw 'Godot project launch would load a different runtime image.'
                }
                if ($arguments -notcontains '-nodeReuse:false' -or $arguments -notcontains '-p:UseSharedCompilation=false') { throw 'Compiler sharing was enabled.' }
            }
            $defaults = @(Get-GodotClientBuildArguments $project)
            if ($defaults[[Array]::IndexOf($defaults, '--configuration') + 1] -cne 'Debug') { throw 'Default client configuration changed.' }
            'PASS'
            """);
        Assert.Contains("PASS", RunPowerShell(script), StringComparison.Ordinal);
    }
}

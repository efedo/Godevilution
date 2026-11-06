using Devilution.Client.Acceptance;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeDepthResumeEvidenceTests
{
    [Fact]
    public void RequiredShrineCannotResumeWithAnOlderCheckpointThatOmitsItsRecord()
    {
        var error = Assert.Throws<InvalidOperationException>(() => NativeDepthResumeEvidence.Verify(
            requireShrine: true, hasShrine: false, requireTrappedDoor: false, 0, 0, 0));
        Assert.Contains("required shrine", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0, 0, 0)] // No object record, or a chest-only checkpoint.
    [InlineData(4, 0, 0)] // A door identity alone does not prove that its trap fired.
    [InlineData(4, 5, 0)]
    public void RequiredTrappedDoorCannotResumeWithoutObservedFiring(uint door, uint source, ulong tick)
    {
        var error = Assert.Throws<InvalidOperationException>(() => NativeDepthResumeEvidence.Verify(
            requireShrine: false, hasShrine: false, requireTrappedDoor: true, door, source, tick));
        Assert.Contains("required trapped door", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RequiredThemeObjectCannotResumeWithoutItsRecord()
    {
        var error = Assert.Throws<InvalidOperationException>(() => NativeDepthResumeEvidence.VerifyThemeObject(true, false));
        Assert.Contains("required theme object", error.Message, StringComparison.OrdinalIgnoreCase);
        NativeDepthResumeEvidence.VerifyThemeObject(true, true);
        NativeDepthResumeEvidence.VerifyThemeObject(false, false);
    }

    [Fact]
    public void OptionalEvidenceKeepsOlderCheckpointsCompatible()
        => NativeDepthResumeEvidence.Verify(false, false, false, 0, 0, 0);

    [Fact]
    public void RequiredEvidenceAllowsRetainedStateVerificationToContinue()
        => NativeDepthResumeEvidence.Verify(true, true, true, 4, 5, 6);
}

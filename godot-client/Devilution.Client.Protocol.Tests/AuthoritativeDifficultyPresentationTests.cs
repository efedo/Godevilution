using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeDifficultyPresentationTests
{
    [Theory]
    [InlineData(GameDifficulty.Unspecified, "Normal")]
    [InlineData(GameDifficulty.Normal, "Normal")]
    [InlineData(GameDifficulty.Nightmare, "Nightmare")]
    [InlineData(GameDifficulty.Hell, "Hell")]
    public void FormatsTheServerSelectedDifficulty(GameDifficulty difficulty, string expected)
    {
        Assert.Equal(expected, AuthoritativeDifficultyPresentation.Format(difficulty));
    }
}

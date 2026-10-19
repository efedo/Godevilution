using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class LocalSessionBookmarkTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Godevilution-Bookmark-" + Guid.NewGuid().ToString("N"));
    private string BookmarkPath => Path.Combine(directory, "nested", "resume-token.txt");

    [Fact]
    public void MissingBookmarkDoesNotCreateASession()
        => Assert.Null(LocalSessionBookmark.Read(BookmarkPath));

    [Fact]
    public void RoundTripsAndAtomicallyReplacesCredentialWithoutGameplayState()
    {
        LocalSessionBookmark.Write(BookmarkPath, "first-token");
        Assert.Equal("first-token", LocalSessionBookmark.Read(BookmarkPath));
        LocalSessionBookmark.Write(BookmarkPath, "second-token");
        Assert.Equal("second-token", LocalSessionBookmark.Read(BookmarkPath));
        Assert.Single(Directory.EnumerateFiles(Path.GetDirectoryName(BookmarkPath)!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid token")]
    [InlineData("invalid\ntoken")]
    public void InvalidWriteDoesNotOverwriteExistingBookmark(string token)
    {
        LocalSessionBookmark.Write(BookmarkPath, "valid-token");
        Assert.Throws<InvalidDataException>(() => LocalSessionBookmark.Write(BookmarkPath, token));
        Assert.Equal("valid-token", LocalSessionBookmark.Read(BookmarkPath));
    }

    [Fact]
    public void RejectsOversizedOrCorruptBookmarksWithoutRepairingThem()
    {
        LocalSessionBookmark.Write(BookmarkPath, "valid-token");
        Assert.Throws<InvalidDataException>(() => LocalSessionBookmark.Write(BookmarkPath, new string('a', 513)));
        File.WriteAllText(BookmarkPath, "bad token");
        Assert.Throws<InvalidDataException>(() => LocalSessionBookmark.Read(BookmarkPath));
        Assert.Equal("bad token", File.ReadAllText(BookmarkPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}

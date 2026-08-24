using HugoMatters.Core.Security;

namespace HugoMatters.Core.Tests.Security;

public class ContentPathValidatorTests
{
    private static readonly IReadOnlyList<string> AllowedDirectories =
        ContentPathValidator.BuildAllowedDirectories("content/posts", "content");

    [Fact]
    public void ValidateAndNormalize_NormalizesBackslashes()
    {
        var normalized = ContentPathValidator.ValidateAndNormalize(
            @"content\posts\my-post.md",
            AllowedDirectories);

        Assert.Equal("content/posts/my-post.md", normalized);
    }

    [Fact]
    public void ValidateAndNormalize_RejectsLeadingSlash()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ContentPathValidator.ValidateAndNormalize("/content/posts/x.md", AllowedDirectories));

        Assert.Contains("repo-relative", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAndNormalize_RejectsParentTraversal()
    {
        Assert.Throws<ArgumentException>(() =>
            ContentPathValidator.ValidateAndNormalize("content/../secret.md", AllowedDirectories));
    }

    [Fact]
    public void ValidateAndNormalize_RejectsCurrentDirectorySegment()
    {
        Assert.Throws<ArgumentException>(() =>
            ContentPathValidator.ValidateAndNormalize("content/./post.md", AllowedDirectories));
    }

    [Fact]
    public void ValidateAndNormalize_RejectsPathsOutsideAllowedDirectories()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ContentPathValidator.ValidateAndNormalize("static/image.png", AllowedDirectories));

        Assert.Contains("outside allowed", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IsUnderAllowedDirectory_ReturnsTrueForNestedPath()
    {
        var result = ContentPathValidator.IsUnderAllowedDirectory(
            "content/posts/2024/hello.md",
            AllowedDirectories);

        Assert.True(result);
    }

    [Fact]
    public void BuildAllowedDirectories_OmitsEmptyEntries()
    {
        var directories = ContentPathValidator.BuildAllowedDirectories("content/posts", null);

        Assert.Single(directories);
        Assert.Equal("content/posts", directories[0]);
    }

    [Fact]
    public void ValidateAndNormalize_AllowsEmptyAllowlist()
    {
        var normalized = ContentPathValidator.ValidateAndNormalize(
            "anywhere/file.md",
            Array.Empty<string>());

        Assert.Equal("anywhere/file.md", normalized);
    }
}

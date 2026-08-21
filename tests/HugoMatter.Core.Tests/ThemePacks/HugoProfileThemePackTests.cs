using HugoMatter.Core.Models;
using HugoMatter.ThemePacks.Packs;

namespace HugoMatter.Core.Tests.ThemePacks;

public class HugoProfileThemePackTests
{
    private static ThemePackDefinition LoadProfilePack()
    {
        var assembly = typeof(HugoProfileThemePack).Assembly;
        using var stream = assembly.GetManifestResourceStream(HugoProfileThemePack.EmbeddedResourceName)
            ?? throw new InvalidOperationException("Embedded profile pack was not found.");
        using var reader = new StreamReader(stream);
        return HugoProfileThemePack.BindFromJson(reader.ReadToEnd());
    }

    [Fact]
    public void BindFromJson_ProducesHugoProfileDefinition()
    {
        var pack = LoadProfilePack();

        Assert.Equal(HugoProfileThemePack.PackId, pack.Id);
        Assert.Equal("Hugo Profile", pack.DisplayName);
        Assert.Equal("content/posts", pack.PostsDirectory);
        Assert.Equal("content", pack.PagesDirectory);
        Assert.Contains(pack.ContentTypes, ct => ct.Id == "post");
        Assert.Contains(pack.SiteConfigFields, f => f.Key == "params.hero.title");
    }

    [Fact]
    public void BindFromJson_AppliesPostDefaults()
    {
        var pack = LoadProfilePack();
        var postType = pack.ContentTypes.First(ct => ct.Id == "post");

        Assert.NotNull(postType.Defaults);
        Assert.True(postType.Defaults!.ContainsKey("draft"));
        Assert.Equal(true, postType.Defaults["draft"]);
    }

    [Fact]
    public void ProbeCompatibility_MatchesThemeNameInHugoConfig()
    {
        var pack = LoadProfilePack();
        var context = new ThemePackProbeContext
        {
            HugoConfigContent = "theme = \"hugo-profile\"",
        };

        var result = HugoProfileThemePack.ProbeCompatibility(pack, context);

        Assert.True(result.IsCompatible);
        Assert.Contains("hugo-profile", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProbeCompatibility_MatchesRequiredRepositoryMarker()
    {
        var pack = LoadProfilePack();
        var context = new ThemePackProbeContext
        {
            RepositoryFilePaths = ["themes/hugo-profile/layouts/index.html"],
        };

        var result = HugoProfileThemePack.ProbeCompatibility(pack, context);

        Assert.True(result.IsCompatible);
    }

    [Fact]
    public void ProbeCompatibility_ReturnsFalseWhenNoMarkersFound()
    {
        var pack = LoadProfilePack();
        var context = new ThemePackProbeContext
        {
            HugoConfigContent = "theme = \"other-theme\"",
            RepositoryFilePaths = ["content/posts/hello.md"],
        };

        var result = HugoProfileThemePack.ProbeCompatibility(pack, context);

        Assert.False(result.IsCompatible);
    }
}

using HugoMatters.Core.Models;
using HugoMatters.Core.Ports;
using HugoMatters.ThemePacks;
using HugoMatters.ThemePacks.Packs;

namespace HugoMatters.Core.Tests.ThemePacks;

public class ThemePackRegistryTests
{
    [Fact]
    public void BuiltInRegistry_RegistersHugoProfilePack()
    {
        var registry = new ThemePackRegistry();

        var packs = registry.ListPacks();
        var detail = registry.GetPackDetail(HugoProfileThemePack.PackId);

        Assert.Contains(packs, pack => pack.Id == HugoProfileThemePack.PackId);
        Assert.NotNull(detail);
        Assert.Equal("Hugo Profile", detail!.DisplayName);
    }

    [Fact]
    public void CustomRegistry_CanRegisterSecondStubPackWithoutCoreChanges()
    {
        var profilePack = new ThemePackRegistry().GetPackDetail(HugoProfileThemePack.PackId)!;
        var stubPack = new ThemePackDefinition
        {
            Id = "stub-pack",
            Version = "0.0.1",
            DisplayName = "Stub Pack",
            ContentTypes =
            [
                new ContentTypeDefinition
                {
                    Id = "post",
                    Label = "Post",
                    Fields = [],
                    Defaults = new Dictionary<string, object?> { ["draft"] = false },
                },
            ],
            PostsDirectory = "posts",
            PagesDirectory = "pages",
        };

        IThemePackRegistry registry = new MultiPackRegistry(profilePack, stubPack);

        Assert.Equal(2, registry.ListPacks().Count);
        Assert.NotNull(registry.GetPackDetail("stub-pack"));
        Assert.Equal("posts", registry.GetPackDetail("stub-pack")!.PostsDirectory);
        Assert.NotNull(registry.GetPackDetail(HugoProfileThemePack.PackId));
    }

    [Fact]
    public void ProbeCompatibility_UsesRegisteredPackDefinition()
    {
        var registry = new ThemePackRegistry();
        var context = new ThemePackProbeContext
        {
            HugoConfigContent = "theme: hugo-profile",
        };

        var result = registry.ProbeCompatibility(HugoProfileThemePack.PackId, context);

        Assert.NotNull(result);
        Assert.True(result!.IsCompatible);
    }

    private sealed class MultiPackRegistry : IThemePackRegistry
    {
        private readonly Dictionary<string, ThemePackDefinition> _packs;

        public MultiPackRegistry(params ThemePackDefinition[] packs)
        {
            _packs = packs.ToDictionary(p => p.Id, StringComparer.Ordinal);
        }

        public IReadOnlyList<ThemePackSummary> ListPacks() =>
            _packs.Values.Select(p => new ThemePackSummary
            {
                Id = p.Id,
                Version = p.Version,
                DisplayName = p.DisplayName,
            }).ToList();

        public ThemePackSummary? GetPack(string id) =>
            _packs.TryGetValue(id, out var pack)
                ? new ThemePackSummary
                {
                    Id = pack.Id,
                    Version = pack.Version,
                    DisplayName = pack.DisplayName,
                }
                : null;

        public ThemePackDefinition? GetPackDetail(string id) =>
            _packs.GetValueOrDefault(id);
    }
}

using HugoMatter.Core.Models;

namespace HugoMatter.Core.Ports;

/// <summary>
/// Registry of versioned theme packs shipped with the product.
/// </summary>
/// <remarks>
/// <para><b>Extension contract</b></para>
/// <para>
/// Theme packs are product-owned modules that supply editor field schemas, content-type
/// defaults, path conventions, and minimal site-config surfaces. Packs are registered in
/// <c>HugoMatter.ThemePacks</c> and discovered via this port; core session/content flows
/// must not hard-code pack-specific types.
/// </para>
/// <para>
/// To add a new pack:
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// Ship a JSON definition conforming to <c>contracts/theme-pack-schema.json</c> as an embedded
/// resource (or equivalent) in the ThemePacks assembly.
/// </description>
/// </item>
/// <item>
/// <description>
/// Implement a binder that deserializes the JSON into <see cref="ThemePackDefinition"/> and
/// optionally provides a compatibility probe (theme names, required files).
/// </description>
/// </item>
/// <item>
/// <description>
/// Register the pack in the ThemePacks registry implementation so
/// <see cref="ListPacks"/> and <see cref="GetPack"/> return it.
/// </description>
/// </item>
/// <item>
/// <description>
/// Core services consume packs only through this interface and
/// <see cref="ThemePackDefinition"/> — no Profile-specific logic in Core.
/// </description>
/// </item>
/// </list>
/// </remarks>
public interface IThemePackRegistry
{
    /// <summary>
    /// Lists all available theme packs.
    /// </summary>
    IReadOnlyList<ThemePackSummary> ListPacks();

    /// <summary>
    /// Gets summary metadata for a pack by id.
    /// </summary>
    ThemePackSummary? GetPack(string id);

    /// <summary>
    /// Gets the full pack definition including content types and site config fields.
    /// </summary>
    ThemePackDefinition? GetPackDetail(string id);
}

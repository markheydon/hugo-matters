using System.Collections.Specialized;
using System.Text.Json;
using HugoMatters.Core.Content;

namespace HugoMatters.Core.Tests.Content;

public class HugoContentDocumentTests
{
    private const string SampleContent = """
---
title: Hello World
date: 2024-01-15T10:30:00Z
draft: true
tags:
  - hugo
  - cms
custom_field: preserved
---
# Heading

Body text with **bold**.
""";

    [Fact]
    public void Parse_ExtractsFrontMatterAndBody()
    {
        var document = HugoContentDocument.Parse(SampleContent);

        Assert.Equal("Hello World", document.FrontMatter["title"]);
        Assert.Equal(true, document.FrontMatter["draft"]);
        Assert.Equal("preserved", document.FrontMatter["custom_field"]);
        Assert.StartsWith("# Heading", document.Body);
    }

    [Fact]
    public void Serialize_RoundTripsFrontMatterFidelity()
    {
        var original = HugoContentDocument.Parse(SampleContent);
        var serialized = original.Serialize();
        var roundTripped = HugoContentDocument.Parse(serialized);

        Assert.Equal(original.FrontMatter["title"], roundTripped.FrontMatter["title"]);
        Assert.Equal(original.FrontMatter["draft"], roundTripped.FrontMatter["draft"]);
        Assert.Equal(original.FrontMatter["custom_field"], roundTripped.FrontMatter["custom_field"]);
        Assert.Equal(original.Body, roundTripped.Body);
    }

    [Fact]
    public void Serialize_WithoutFrontMatter_ReturnsBodyOnly()
    {
        var document = HugoContentDocument.Create(new OrderedDictionary(StringComparer.Ordinal), "Just markdown.");

        Assert.Equal("Just markdown.", document.Serialize());
    }

    [Fact]
    public void Parse_WithoutFrontMatterDelimiter_TreatsEntireContentAsBody()
    {
        var document = HugoContentDocument.Parse("No frontmatter here.");

        Assert.Empty(document.FrontMatter);
        Assert.Equal("No frontmatter here.", document.Body);
    }

    [Fact]
    public void Parse_PreservesUnknownFrontmatterKeys()
    {
        const string content = """
---
title: Test
legacy_key: legacy-value
---
Body
""";

        var document = HugoContentDocument.Parse(content);

        Assert.Equal("legacy-value", document.FrontMatter["legacy_key"]);
        var serialized = document.Serialize();
        Assert.Contains("legacy_key", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_AllowsProgrammaticRoundTrip()
    {
        var frontMatter = new OrderedDictionary(StringComparer.Ordinal)
        {
            ["title"] = "Created",
            ["draft"] = false,
            ["count"] = 3,
        };

        var document = HugoContentDocument.Create(frontMatter, "Created body.");
        var parsed = HugoContentDocument.Parse(document.Serialize());

        Assert.Equal("Created", parsed.FrontMatter["title"]);
        Assert.Equal(false, parsed.FrontMatter["draft"]);
        Assert.Equal(3, parsed.FrontMatter["count"]);
        Assert.Equal("Created body.", parsed.Body);
    }

    [Fact]
    public void Serialize_JsonElementArrayTags_WritesYamlSequence()
    {
        using var tagsJson = JsonDocument.Parse("""["Turpin Enterprises Journal"]""");
        var frontMatter = new OrderedDictionary(StringComparer.Ordinal)
        {
            ["title"] = "Post",
            ["tags"] = tagsJson.RootElement.Clone(),
        };

        var serialized = HugoContentDocument.Create(frontMatter, "Body").Serialize();

        Assert.Contains("tags:", serialized, StringComparison.Ordinal);
        Assert.Contains("- Turpin Enterprises Journal", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("""["Turpin Enterprises Journal"]""", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_RepairsStringifiedJsonArrayTags()
    {
        const string corrupted = """
---
title: Post
tags: '["Turpin Enterprises Journal"]'
---
Body
""";

        var serialized = HugoContentDocument.Parse(corrupted).Serialize();

        Assert.Contains("- Turpin Enterprises Journal", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("""["Turpin Enterprises Journal"]""", serialized, StringComparison.Ordinal);
    }
}

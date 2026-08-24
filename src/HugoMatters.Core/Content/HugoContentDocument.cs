using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;
using System.Text.Json;
using YamlDotNet.RepresentationModel;

namespace HugoMatters.Core.Content;

/// <summary>
/// Parses and serializes Hugo content files with YAML frontmatter and a Markdown body.
/// </summary>
public sealed class HugoContentDocument
{
    private HugoContentDocument(OrderedDictionary frontMatter, string body)
    {
        FrontMatter = frontMatter;
        Body = body;
    }

    /// <summary>Ordered frontmatter fields; unknown keys are preserved.</summary>
    public OrderedDictionary FrontMatter { get; }

    /// <summary>Markdown body after frontmatter.</summary>
    public string Body { get; }

    /// <summary>
    /// Creates a document from frontmatter and body.
    /// </summary>
    public static HugoContentDocument Create(OrderedDictionary frontMatter, string body) =>
        new(frontMatter, body);

    /// <summary>
    /// Parses a Hugo content file string into frontmatter and body.
    /// </summary>
    public static HugoContentDocument Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.StartsWith("---", StringComparison.Ordinal))
        {
            return new HugoContentDocument(new OrderedDictionary(StringComparer.Ordinal), content);
        }

        var endIndex = FindFrontMatterEnd(content);
        if (endIndex < 0)
        {
            return new HugoContentDocument(new OrderedDictionary(StringComparer.Ordinal), content);
        }

        var yamlText = content[3..endIndex].Trim('\r', '\n');
        var body = content[(endIndex + 3)..].TrimStart('\r', '\n');

        var frontMatter = ParseYamlToOrderedDictionary(yamlText);
        return new HugoContentDocument(frontMatter, body);
    }

    /// <summary>
    /// Serializes frontmatter and body back to a Hugo content file string.
    /// </summary>
    public string Serialize()
    {
        if (FrontMatter.Count == 0)
        {
            return Body;
        }

        var yaml = SerializeOrderedDictionary(FrontMatter);
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.Append(yaml.TrimEnd());
        builder.AppendLine();
        builder.AppendLine("---");
        if (!string.IsNullOrEmpty(Body))
        {
            builder.Append(Body);
        }

        return builder.ToString();
    }

    private static int FindFrontMatterEnd(string content)
    {
        var index = 3;
        while (index < content.Length)
        {
            if (content.AsSpan(index).StartsWith("\r\n---"))
            {
                return index + 2;
            }

            if (content.AsSpan(index).StartsWith("\n---"))
            {
                return index + 1;
            }

            index++;
        }

        return -1;
    }

    private static OrderedDictionary ParseYamlToOrderedDictionary(string yamlText)
    {
        var result = new OrderedDictionary(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(yamlText))
        {
            return result;
        }

        var stream = new YamlStream();
        using var reader = new StringReader(yamlText);
        stream.Load(reader);

        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode mapping)
        {
            return result;
        }

        foreach (var entry in mapping.Children)
        {
            var key = entry.Key.ToString();
            result[key] = ConvertYamlNode(entry.Value);
        }

        return result;
    }

    private static object? ConvertYamlNode(YamlNode node) => node switch
    {
        YamlScalarNode scalar => ConvertScalar(scalar),
        YamlSequenceNode sequence => sequence.Children.Select(ConvertYamlNode).ToList(),
        YamlMappingNode mapping => mapping.Children.ToDictionary(
            e => e.Key.ToString(),
            e => ConvertYamlNode(e.Value),
            StringComparer.Ordinal),
        _ => node.ToString(),
    };

    private static object? ConvertScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value;
        if (value is null)
        {
            return null;
        }

        if (bool.TryParse(value, out var boolValue))
        {
            return boolValue;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return intValue;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
        {
            return doubleValue;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateValue))
        {
            return dateValue;
        }

        return value;
    }

    private static string SerializeOrderedDictionary(OrderedDictionary dictionary)
    {
        var root = new YamlMappingNode();
        foreach (DictionaryEntry entry in dictionary)
        {
            var key = entry.Key?.ToString() ?? string.Empty;
            root.Add(new YamlScalarNode(key), ConvertToYamlNode(entry.Value));
        }

        var stream = new YamlStream(new YamlDocument(root));
        using var writer = new StringWriter();
        stream.Save(writer, assignAnchors: false);
        return writer.ToString();
    }

    private static YamlNode ConvertToYamlNode(object? value) => value switch
    {
        null => new YamlScalarNode(string.Empty) { Style = YamlDotNet.Core.ScalarStyle.Plain },
        bool b => new YamlScalarNode(b ? "true" : "false"),
        string s => ConvertStringToYamlNode(s),
        JsonElement json => ConvertJsonElementToYamlNode(json),
        int or long or short or byte => new YamlScalarNode(Convert.ToString(value, CultureInfo.InvariantCulture)),
        float or double or decimal => new YamlScalarNode(Convert.ToString(value, CultureInfo.InvariantCulture)),
        DateTime dt => new YamlScalarNode(dt.ToString("o", CultureInfo.InvariantCulture)),
        DateTimeOffset dto => new YamlScalarNode(dto.ToString("o", CultureInfo.InvariantCulture)),
        IDictionary dict => new YamlMappingNode(
            dict.Cast<DictionaryEntry>()
                .Select(e => new KeyValuePair<YamlNode, YamlNode>(
                    new YamlScalarNode(e.Key?.ToString() ?? string.Empty),
                    ConvertToYamlNode(e.Value)))),
        // Non-string enumerables must stay sequences (tags, authors, etc.). Match after
        // IDictionary so maps are not flattened, and exclude string.
        IEnumerable enumerable when value is not string => new YamlSequenceNode(
            enumerable.Cast<object?>().Select(ConvertToYamlNode)),
        _ => new YamlScalarNode(value.ToString()),
    };

    /// <summary>
    /// Repairs string values that are actually JSON arrays (e.g. <c>["a","b"]</c>) produced when
    /// <see cref="JsonElement"/> arrays were previously stringified into frontmatter.
    /// </summary>
    private static YamlNode ConvertStringToYamlNode(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[^1] == ']')
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    return ConvertJsonElementToYamlNode(doc.RootElement);
                }
            }
            catch (JsonException)
            {
                // Not JSON — keep as a plain scalar.
            }
        }

        return new YamlScalarNode(value);
    }

    private static YamlNode ConvertJsonElementToYamlNode(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined =>
            new YamlScalarNode(string.Empty) { Style = YamlDotNet.Core.ScalarStyle.Plain },
        JsonValueKind.True => new YamlScalarNode("true"),
        JsonValueKind.False => new YamlScalarNode("false"),
        JsonValueKind.Number => new YamlScalarNode(element.GetRawText()),
        JsonValueKind.String => ConvertStringToYamlNode(element.GetString() ?? string.Empty),
        JsonValueKind.Array => new YamlSequenceNode(element.EnumerateArray().Select(ConvertJsonElementToYamlNode)),
        JsonValueKind.Object => new YamlMappingNode(
            element.EnumerateObject()
                .Select(p => new KeyValuePair<YamlNode, YamlNode>(
                    new YamlScalarNode(p.Name),
                    ConvertJsonElementToYamlNode(p.Value)))),
        _ => new YamlScalarNode(element.ToString()),
    };
}

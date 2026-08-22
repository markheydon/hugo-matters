using System.Text.Json;
using System.Text.Json.Serialization;
using HugoMatters.Core.Models;

namespace HugoMatters.Core.Json;

/// <summary>
/// Serializes <see cref="ContentTypeKind"/> as lowercase strings (<c>post</c>, <c>page</c>).
/// </summary>
public sealed class ContentTypeKindJsonConverter : JsonConverter<ContentTypeKind>
{
    /// <inheritdoc />
    public override ContentTypeKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.Equals(value, "post", StringComparison.OrdinalIgnoreCase))
        {
            return ContentTypeKind.Post;
        }

        if (string.Equals(value, "page", StringComparison.OrdinalIgnoreCase))
        {
            return ContentTypeKind.Page;
        }

        throw new JsonException($"Unknown content type '{value}'.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ContentTypeKind value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value == ContentTypeKind.Post ? "post" : "page");
    }
}

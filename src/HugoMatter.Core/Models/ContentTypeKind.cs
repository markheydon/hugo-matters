using System.Text.Json.Serialization;
using HugoMatter.Core.Json;

namespace HugoMatter.Core.Models;

/// <summary>
/// Kind of Hugo content item edited in a session.
/// </summary>
[JsonConverter(typeof(ContentTypeKindJsonConverter))]
public enum ContentTypeKind
{
    /// <summary>A blog post or article.</summary>
    Post,

    /// <summary>A static page.</summary>
    Page,
}

using System.Text.Json.Serialization;

namespace HugoMatters.Web.Models;

public sealed class ErrorBody
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

public sealed class AuthorizeRequest
{
    [JsonPropertyName("installationId")]
    public long? InstallationId { get; init; }

    [JsonPropertyName("owner")]
    public string? Owner { get; init; }

    [JsonPropertyName("repo")]
    public string? Repo { get; init; }

    [JsonPropertyName("callbackCode")]
    public string? CallbackCode { get; init; }
}

public sealed class AuthorizeResponse
{
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("redirectUrl")]
    public string? RedirectUrl { get; init; }

    [JsonPropertyName("site")]
    public ConnectedSiteDto? Site { get; init; }
}

public sealed class ConnectedSiteDto
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("ownerLogin")]
    public required string OwnerLogin { get; init; }

    [JsonPropertyName("repoName")]
    public required string RepoName { get; init; }

    [JsonPropertyName("defaultBranch")]
    public required string DefaultBranch { get; init; }

    [JsonPropertyName("htmlUrl")]
    public string? HtmlUrl { get; init; }

    [JsonPropertyName("themePackId")]
    public required string ThemePackId { get; init; }

    [JsonPropertyName("themePackVersion")]
    public string? ThemePackVersion { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }
}

public sealed class EditingSessionDto
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("siteId")]
    public required Guid SiteId { get; init; }

    [JsonPropertyName("branchName")]
    public required string BranchName { get; init; }

    [JsonPropertyName("pullRequestNumber")]
    public required int PullRequestNumber { get; init; }

    [JsonPropertyName("pullRequestUrl")]
    public string? PullRequestUrl { get; init; }

    [JsonPropertyName("baseBranch")]
    public required string BaseBranch { get; init; }

    [JsonPropertyName("state")]
    public required string State { get; init; }

    [JsonPropertyName("hasUnsavedLocalEdits")]
    public required bool HasUnsavedLocalEdits { get; init; }
}

public sealed class SaveRequest
{
    [JsonPropertyName("commitMessage")]
    public string? CommitMessage { get; init; }
}

public sealed class SaveResultDto
{
    [JsonPropertyName("commitSha")]
    public required string CommitSha { get; init; }

    [JsonPropertyName("hasUnsavedLocalEdits")]
    public required bool HasUnsavedLocalEdits { get; init; }
}

public sealed class PublishResultDto
{
    [JsonPropertyName("outcome")]
    public required string Outcome { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("mergeCommitSha")]
    public string? MergeCommitSha { get; init; }
}

public sealed class DiscardRequest
{
    [JsonPropertyName("confirmDiscardUnsaved")]
    public required bool ConfirmDiscardUnsaved { get; init; }
}

public sealed class DiscardResultDto
{
    [JsonPropertyName("outcome")]
    public required string Outcome { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

public sealed class ContentItemSummaryDto
{
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("contentType")]
    public required string ContentType { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("isDeleted")]
    public required bool IsDeleted { get; init; }

    [JsonPropertyName("isNew")]
    public required bool IsNew { get; init; }
}

public sealed class ContentItemDto
{
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("contentType")]
    public required string ContentType { get; init; }

    [JsonPropertyName("frontMatter")]
    public Dictionary<string, object?> FrontMatter { get; init; } = new();

    [JsonPropertyName("body")]
    public required string Body { get; init; }

    [JsonPropertyName("isDeleted")]
    public required bool IsDeleted { get; init; }

    [JsonPropertyName("isNew")]
    public required bool IsNew { get; init; }

    [JsonPropertyName("hasUnsavedLocalEdits")]
    public required bool HasUnsavedLocalEdits { get; init; }
}

public sealed class ContentItemWrite
{
    [JsonPropertyName("frontMatter")]
    public required Dictionary<string, object?> FrontMatter { get; init; }

    [JsonPropertyName("body")]
    public required string Body { get; init; }
}

public sealed class ContentCreateRequest
{
    [JsonPropertyName("contentType")]
    public required string ContentType { get; init; }

    [JsonPropertyName("slug")]
    public required string Slug { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }
}

public sealed class EditorPreviewRequest
{
    [JsonPropertyName("body")]
    public required string Body { get; init; }

    [JsonPropertyName("frontMatter")]
    public Dictionary<string, object?>? FrontMatter { get; init; }
}

public sealed class EditorPreviewResponse
{
    [JsonPropertyName("html")]
    public required string Html { get; init; }
}

public sealed class SitePreviewDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("baseUrl")]
    public string? BaseUrl { get; init; }

    [JsonPropertyName("sourceRef")]
    public string? SourceRef { get; init; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
}

public class ThemePackSummaryDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }
}

public sealed class FieldDefinitionDto
{
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("dataType")]
    public required string DataType { get; init; }

    [JsonPropertyName("required")]
    public bool Required { get; init; }

    [JsonPropertyName("default")]
    public object? Default { get; init; }

    [JsonPropertyName("editorWidget")]
    public string? EditorWidget { get; init; }

    [JsonPropertyName("options")]
    public List<string>? Options { get; init; }
}

public sealed class ContentTypeDefinitionDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("fields")]
    public List<FieldDefinitionDto> Fields { get; init; } = [];
}

public sealed class ThemePackDetailDto : ThemePackSummaryDto
{
    [JsonPropertyName("contentTypes")]
    public List<ContentTypeDefinitionDto> ContentTypes { get; init; } = [];

    [JsonPropertyName("siteConfigFields")]
    public List<FieldDefinitionDto> SiteConfigFields { get; init; } = [];
}

public sealed class ApiException : Exception
{
    public ApiException(string message, string? code = null, int? statusCode = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string? Code { get; }

    public int? StatusCode { get; }
}

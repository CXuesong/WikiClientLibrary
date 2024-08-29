using System.Text.Json.Serialization;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Wikia.WikiaApi;

/// <summary>
/// See https://github.com/Wikia/app/blob/dev/includes/wikia/api/UserApiController.class.php#L31-L87 .
/// </summary>
[JsonContract]
public sealed class UserInfo
{

    /// <summary>User ID.</summary>
    [JsonPropertyName("user_id")]
    public int Id { get; init; }

    /// <summary>User title. (Often the same as <see cref="Name"/>.)</summary>
    [JsonPropertyName("title")]
    public string Title { get; init; }

    /// <summary>User name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; }

    /// <summary>The full URL of user's page.</summary>
    [JsonPropertyName("url")]
    [JsonInclude]
    public string UserPageUrl { get; private set; }

    [Obsolete("The field has been removed from Wikia v1 API response.")]
    [JsonIgnore]
    public ICollection<string> PowerUserTypes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("is_subject_to_ccpa")]
    public bool? IsSubjectToCcpa { get; init; }

    /// <summary>The full URL of user's avatar.</summary>
    [JsonPropertyName("avatar")]
    [JsonInclude]
    public string AvatarUrl { get; private set; }

    /// <summary>User's number of edits.</summary>
    [JsonPropertyName("numberofedits")]
    public int EditsCount { get; init; }

    internal void ApplyBasePath(string basePath)
    {
        if (UserPageUrl != null) UserPageUrl = MediaWikiHelper.MakeAbsoluteUrl(basePath, UserPageUrl);
        if (AvatarUrl != null) AvatarUrl = MediaWikiHelper.MakeAbsoluteUrl(basePath, AvatarUrl);
    }

    /// <summary>Creates a <see cref="UserStub"/> from the current user information.</summary>
    public UserStub ToUserStub()
    {
        return new UserStub(Name, Id);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }

}

using System.Text.Json.Serialization;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Sites;

/// <summary>
/// Provides read-only access to the current logged-in information.
/// </summary>
/// <remarks>See <a href="https://www.mediawiki.org/wiki/API:Userinfo">mw:API:UserInfo</a>.</remarks>
[JsonContract]
public sealed class AccountInfo
{

    public long Id { get; init; }

    public string Name { get; init; }

    /// <summary>
    /// Determines wheter current user is anonymous.
    /// It's recommended that you use <see cref="IsUser"/> to determine
    /// whether a user has logged in.
    /// </summary>
    [JsonPropertyName("anon")]
    public bool IsAnonymous { get; init; }

    /// <summary>
    /// Determines whether current user is in "user" group.
    /// This is usually used to determine whether a user
    /// has logged in.
    /// </summary>
    public bool IsUser => Groups.Contains(UserGroups.User);

    /// <summary>
    /// Determines whether current user is in "bot" group.
    /// </summary>
    public bool IsBot => Groups.Contains(UserGroups.Bot);

    /// <summary>
    /// Determines whether the current user has been blocked.
    /// </summary>
    public bool IsBlocked => BlockId != 0;

    public int BlockId { get; init; }

    public string BlockedBy { get; init; }

    public int BlockedById { get; init; }

    public string BlockReason { get; init; }

    [JsonPropertyName("blockedtimestamp")]
    public DateTime BlockedSince { get; init; }

    public DateTime BlockExpiry { get; init; }

    public IReadOnlyCollection<string> Groups { get; init; }

    public IReadOnlyCollection<string> Rights { get; init; }

    /// <summary>
    /// Determines whether the user is in certain group.
    /// </summary>
    /// <param name="groupName">The group user should be in.</param>
    /// <remarks>It's recommended to use this method instead of checking <see cref="Groups"/> manually.</remarks>
    public bool IsInGroup(string groupName)
    {
        if (groupName == null) throw new ArgumentNullException(nameof(groupName));
        return Groups.Contains(groupName);
    }

    /// <summary>
    /// Asserts the user is in certain group.
    /// </summary>
    /// <param name="groupName">The group user should be in.</param>
    /// <exception cref="UnauthorizedOperationException">The user is not in the specific group.</exception>
    public void AssertInGroup(string groupName)
    {
        if (groupName == null) throw new ArgumentNullException(nameof(groupName));
        if (!IsInGroup(groupName))
            throw new UnauthorizedOperationException(null, string.Format(Prompts.ExceptionUserNotInGroup1, groupName));
    }

    /// <summary>
    /// Determines whether the user has certian right.
    /// </summary>
    /// <param name="rightName">The name of the right.</param>
    /// <remarks>It's recommended to use this method instead of checking <see cref="Rights"/> manually.</remarks>
    public bool HasRight(string rightName)
    {
        if (rightName == null) throw new ArgumentNullException(nameof(rightName));
        return Rights.Contains(rightName);
    }

    /// <summary>
    /// Asserts the user has certain right.
    /// </summary>
    /// <param name="rightName">The name of the right.</param>
    /// <exception cref="UnauthorizedOperationException">The user doesn't have specific right.</exception>
    public void AssertRight(string rightName)
    {
        if (rightName == null) throw new ArgumentNullException(nameof(rightName));
        if (!HasRight(rightName))
            throw new UnauthorizedOperationException(null, string.Format(Prompts.ExceptionUserNotHaveRight1, rightName));
    }

    /// <summary>
    /// Creates a <see cref="UserStub"/> from the current account information.
    /// </summary>
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

/// <summary>
/// Predefined User Groups.
/// </summary>
public static class UserGroups
{

    public const string User = "user";
    public const string Bot = "bot";
    public const string SysOp = "sysop";
    public const string Autoconfirmed = "autoconfirmed";

}

/// <summary>
/// Predefined User Rights.
/// </summary>
public static class UserRights
{

    public const string ApiHighLimits = "apihighlimits";
    public const string Patrol = "patrol";
    public const string AutoPatrol = "autopatrol";

}

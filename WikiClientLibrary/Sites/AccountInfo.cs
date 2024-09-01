using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    private readonly AccountBlockInfo emptyBlockInfoSentry = new ()
    {
        BlockId = -1,
        BlockedBy = "<dummy>",
        BlockedById = -1,
        BlockReason = "Not blocked.",
    };

    /// <summary>Current user ID.</summary>
    /// <value>user ID, or <c>0</c> if the current user has not logged in yet.</value>
    public long Id { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// Determines whether current user is anonymous.
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
    public bool IsUser => IsInGroup(UserGroups.User);

    /// <summary>
    /// Determines whether current user is in "bot" group.
    /// </summary>
    public bool IsBot => IsInGroup(UserGroups.Bot);

    /// <summary>Determines whether the current user has been blocked.</summary>
    /// <seealso cref="BlockInfo"/>
    [MemberNotNullWhen(true, nameof(BlockInfo))]
    public bool IsBlocked
    {
        get
        {
            var localBlockInfo = this._BlockInfo;
            if (localBlockInfo == null)
            {
                return extensionDataRaw != null
                       && extensionDataRaw.ContainsKey("blockid")
                       && extensionDataRaw.ContainsKey("blockedbyid");
            }
            return localBlockInfo != emptyBlockInfoSentry;
        }
    }

    private AccountBlockInfo? _BlockInfo;

    /// <summary>Detailed information of the active block placed on the current user, if any.</summary>
    /// <seealso cref="IsBlocked"/>
    [JsonIgnore]
    public AccountBlockInfo? BlockInfo
    {
        get
        {
            var localBlockInfo = this._BlockInfo;
            if (localBlockInfo == null)
            {
                // While `blockid` could be `null` for system blocks, the node always exists.
                if (extensionDataRaw != null
                    && extensionDataRaw.ContainsKey("blockid")
                    && extensionDataRaw.ContainsKey("blockedbyid")
                   )
                {
                    var obj = new JsonObject(extensionDataRaw
                        .Where(p => p.Key.Contains("block"))
                        .Select(p => KeyValuePair.Create(p.Key, p.Value.Deserialize<JsonNode>())));
                    localBlockInfo = obj.Deserialize<AccountBlockInfo>(MediaWikiHelper.WikiJsonSerializerOptions);
                }
                else
                {
                    localBlockInfo = emptyBlockInfoSentry;
                }
                var prev = Interlocked.CompareExchange(ref _BlockInfo, localBlockInfo, null);
                if (prev != null) localBlockInfo = prev;
            }
            return localBlockInfo == emptyBlockInfoSentry ? null : localBlockInfo;
        }
    }

    public required IReadOnlyCollection<string> Groups { get; init; }

    public required IReadOnlyCollection<string> Rights { get; init; }

    /// <summary>
    /// Determines whether the user is in certain group.
    /// </summary>
    /// <param name="groupName">The group user should be in.</param>
    /// <remarks>It's recommended to use this method instead of checking <see cref="Groups"/> manually.</remarks>
    public bool IsInGroup(string groupName)
    {
        if (groupName == null) throw new ArgumentNullException(nameof(groupName));
        return Groups.Contains(groupName, StringComparer.Ordinal);
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
    /// Determines whether the user has certain right.
    /// </summary>
    /// <param name="rightName">The name of the right.</param>
    /// <remarks>
    /// <para>
    /// It's recommended to use this method instead of checking <see cref="Rights"/> manually.</para>
    /// <para>Refer to <see cref="UserRights"/> for a list of commonly-used built-in user rights.</para>
    /// </remarks>
    public bool HasRight(string rightName)
    {
        if (rightName == null) throw new ArgumentNullException(nameof(rightName));
        // MW is performing case-sensitive match.
        // https://github.com/wikimedia/mediawiki/blob/684496b6211784197042c3f8e30dc8c7b060c2ae/includes/Permissions/PermissionManager.php#L1488-L1489
        return Rights.Contains(rightName, StringComparer.Ordinal);
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

    private ReadOnlyDictionary<string, JsonElement>? extensionDataWrapper;

    [JsonExtensionData][JsonInclude] private Dictionary<string, JsonElement>? extensionDataRaw;

    /// <summary>
    /// Gets the other extensible site information.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> ExtensionData
    {
        get
        {
            if (extensionDataWrapper != null) { return extensionDataWrapper; }
            // There should almost always be overflowed JSON props. No need to optimize for empty dict.
            var inst = new ReadOnlyDictionary<string, JsonElement>(extensionDataRaw ?? new Dictionary<string, JsonElement>());
            var original = Interlocked.CompareExchange(ref extensionDataWrapper, inst, null);
            return original ?? inst;
        }
    }

}

/// <summary>Predefined User Groups.</summary>
public static class UserGroups
{

    public const string User = "user";
    public const string Bot = "bot";
    public const string SysOp = "sysop";
    [Obsolete("Use UserGroups.AutoConfirmed instead")] public const string Autoconfirmed = "autoconfirmed";
    public const string AutoConfirmed = "autoconfirmed";

}

/// <summary>Predefined User Rights.</summary>
/// <seealso cref="AccountInfo.HasRight"/>
/// <seealso cref="AccountInfo.Rights"/>
public static class UserRights
{

    // Basic rights
    public const string Read = "read";
    public const string Edit = "edit";
    public const string Move = "move";
    public const string Upload = "upload";
    public const string CreateAccount = "createaccount";
    public const string CreatePage = "createpage";
    public const string CreateTalk = "createtalk";
    // Privileged rights
    public const string ApiHighLimits = "apihighlimits";
    public const string Patrol = "patrol";
    public const string AutoPatrol = "autopatrol";

}

/// <summary>
/// Contains basic information about a given account block.
/// </summary>
/// <remarks>
/// See <a href="https://github.com/wikimedia/mediawiki/blob/master/includes/api/ApiBlockInfoTrait.php">ApiBlockInfoTrait.php</a>.
/// </remarks>
/// <seealso cref="AccountInfo"/>
[JsonContract]
public sealed class AccountBlockInfo
{

    /// <summary>ID of the block.</summary>
    /// <value>block ID, or <c>null</c> if the block is a system block (see <see cref="SystemBlockType"/>).</value>
    public required long? BlockId { get; init; }

    /// <summary>Username of the blocker.</summary>
    /// <value>username, or empty string (<c>""</c>) if the block is a system block.</value>
    public required string BlockedBy { get; init; }

    /// <summary>User ID of the blocker.</summary>
    /// <value>username, or <c>0</c> if the block is a system block.</value>
    public required long BlockedById { get; init; }

    /// <summary>Reason provided for the block, in Wikitext.</summary>
    public string? BlockReason { get; init; }

    /// <summary>Whether the block only applies to certain pages, namespaces and/or actions</summary>
    [JsonPropertyName("blockpartial")]
    public bool IsPartialBlock { get; init; }

    /// <summary>Whether the account creation has also been blocked.</summary>
    [JsonPropertyName("blocknocreate")]
    public bool IsCreateAccountBlocked { get; init; }

    /// <summary>Whether the block only affects anonymous users.</summary>
    /// <remarks>
    /// <para>If the value of this property is <c>false</c>, the block is a "hard block" (affects logged-in users on a given IP/range).
    /// Note that temporary users are not considered logged-in here - they are always blocked by IP-address blocks.
    /// Note that user blocks are always hard blocks, since the target is logged in by definition.</para>
    /// <para>See <a href="https://github.com/wikimedia/mediawiki/blob/684496b6211784197042c3f8e30dc8c7b060c2ae/includes/block/Block.php#L163-L173">Block.php</a>
    /// for more information.</para>
    /// </remarks>
    [JsonPropertyName("blockanononly")]
    public bool IsAnonymousBlock { get; init; }

    /// <summary>Whether this block blocks the target from sending emails.</summary>
    [JsonPropertyName("blockemail")]
    public bool IsEmailBlocked { get; init; }

    /// <summary>Whether the block blocks the target from editing their own user talk page.</summary>
    [JsonPropertyName("blockowntalk")]
    public bool IsUserTalkEditBlocked { get; init; }

    /// <summary>Timestamp for when the block was placed/modified.</summary>
    [JsonPropertyName("blockedtimestamp")]
    public DateTime BlockedSince { get; init; }

    /// <summary>Expiry time of the block.</summary>
    public DateTime BlockExpiry { get; init; }

    /// <summary>System block type, if this block is a system block.</summary>
    /// <remarks>Refer to <seealso cref="AccountSystemBlockTypes"/> for a list of possible system block type.</remarks>
    public string? SystemBlockType { get; init; }

    /// <summary>For composite block, retrieve the block components. (MW 1.34+)</summary>
    /// <value>a readonly collection of block information, or <c>null</c> if the block is not a composite block.</value>
    public IReadOnlyCollection<AccountBlockInfo>? BlockComponents { get; init; }

}

/// <summary>Predefined account system block types.</summary>
/// <seealso cref="AccountBlockInfo.SystemBlockType"/>
public static class AccountSystemBlockTypes
{

    /// <summary>the IP is listed in <c>$wgProxyList</c>.</summary>
    public const string Proxy = "proxy";

    /// <summary>the IP is associated with a listed domain in <c>$wgDnsBlacklistUrls</c>.</summary>
    public const string DnsBlacklistUrl = "user";

    /// <summary>the IP is covered by <c>$wgSoftBlockRanges</c>.</summary>
    public const string SoftBlockRanges = "wgSoftBlockRange";

    /// <summary>for backwards compatibility with the <c>UserIsBlockedGlobally</c> hook.</summary>
    public const string GlobalBlock = "global-block";

}

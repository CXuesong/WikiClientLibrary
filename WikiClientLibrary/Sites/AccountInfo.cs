using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace WikiClientLibrary.Sites
{
    /// <summary>
    /// Provides read-only access to the current logged-in information.
    /// </summary>
    /// <remarks>See <a href="https://www.mediawiki.org/wiki/API:Userinfo">mw:API:UserInfo</a>.</remarks>
    [JsonObject(MemberSerialization.OptIn)]
    public class AccountInfo
    {

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        internal AccountInfo()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        {
        }

        [JsonProperty]
        public int Id { get; private set; }

        [JsonProperty]
        public string Name { get; private set; }

        /// <summary>
        /// Determines wheter current user is anonymous.
        /// It's recommended that you use <see cref="IsUser"/> to determine
        /// whether a user has logged in.
        /// </summary>
        [JsonProperty("anon")]
        public bool IsAnonymous { get; private set; }

        /// <summary>
        /// Determines wheter current user is in "user" group.
        /// This is usually used to determine whether a user
        /// has logged in.
        /// </summary>
        public bool IsUser => Groups.Contains(UserGroups.User);

        /// <summary>
        /// Determines wheter current user is in "bot" group.
        /// </summary>
        public bool IsBot => Groups.Contains(UserGroups.Bot);

        /// <summary>
        /// Determins whether the current user has been blocked.
        /// </summary>
        public bool IsBlocked => BlockId != 0;

        [JsonProperty]
        public int BlockId { get; private set; }

        [JsonProperty]
        public string BlockedBy { get; private set; }

        [JsonProperty]
        public int BlockedById { get; private set; }

        [JsonProperty]
        public string BlockReason { get; private set; }

        [JsonProperty("blockedtimestamp")]
        public DateTime BlockedSince { get; private set; }

        [JsonProperty]
        public DateTime BlockExpiry { get; private set; }

        [JsonProperty]
        public IReadOnlyCollection<string> Groups { get; private set; }

        [JsonProperty]
        public IReadOnlyCollection<string> Rights { get; private set; }

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
}

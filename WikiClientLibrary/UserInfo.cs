using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WikiClientLibrary
{
    /// <summary>
    /// Provides read-only access to the current logged-in information.
    /// </summary>
    /// <remarks>See https://www.mediawiki.org/wiki/API:Userinfo .</remarks>
    [JsonObject(MemberSerialization.OptIn)]
    public class UserInfo
    {
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
        /// Determines whether the user is in certian group.
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
                throw new UnauthorizedOperationException($"Current user is not in the group: {groupName}.");
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
        /// Asserts the user has certian right.
        /// </summary>
        /// <param name="rightName">The name of the right.</param>
        /// <exception cref="UnauthorizedOperationException">The user doesn't have specific right.</exception>
        public void AssertRight(string rightName)
        {
            if (rightName == null) throw new ArgumentNullException(nameof(rightName));
            if (!HasRight(rightName))
                throw new UnauthorizedOperationException($"Current user doesn't have the right: {rightName}.");
        }

        internal UserInfo()
        {
            
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

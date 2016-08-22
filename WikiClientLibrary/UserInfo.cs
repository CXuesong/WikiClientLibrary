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

        [JsonProperty("anon")]
        public bool IsAnnonymous { get; private set; }

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
        /// Asserts the user is in certain group.
        /// </summary>
        /// <param name="groupName">The group user should be in.</param>
        /// <exception cref="UnauthorizedOperationException">The user is not in the specific group.</exception>
        public void AssertInGroup(string groupName)
        {
            if (groupName == null) throw new ArgumentNullException(nameof(groupName));
            if (!Groups.Contains(groupName))
                throw new UnauthorizedOperationException($"Current user is not in the group:{groupName}.");
        }

        internal UserInfo()
        {
            
        }
    }

    public static class UserGroups
    {
        public const string User = "user";
        public const string Autoconfirmed = "autoconfirmed";
    }
}

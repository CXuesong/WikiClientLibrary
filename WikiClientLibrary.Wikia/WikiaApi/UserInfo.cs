using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Wikia.WikiaApi
{
    [JsonObject(MemberSerialization.OptIn)]
    public class UserInfo
    {

        /// <summary>User ID.</summary>
        [JsonProperty("user_id")]
        public int Id { get; private set; }

        /// <summary>User title. (Often the same as <see cref="Name"/>.)</summary>
        [JsonProperty("title")]
        public string Title { get; private set; }

        /// <summary>User name.</summary>
        [JsonProperty("name")]
        public string Name { get; private set; }

        /// <summary>The full URL of user's page.</summary>
        [JsonProperty("url")]
        public string UserPageUrl { get; private set; }

        [JsonProperty("poweruser_types")]
        public ICollection<string> PowerUserTypes { get; private set; } = Array.Empty<string>();

        /// <summary>The full URL of user's avatar.</summary>
        [JsonProperty("avatar")]
        public string AvatarUrl { get; private set; }

        /// <summary>User's number of edits.</summary>
        [JsonProperty("numberofedits")]
        public int EditsCount { get; private set; }

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
}

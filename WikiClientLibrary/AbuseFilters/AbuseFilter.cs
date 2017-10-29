using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Newtonsoft.Json;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.AbuseFilters
{
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class AbuseFilter
    {

        public static readonly string[] emptyActions = { };

        public WikiSite Site { get; }

        [JsonProperty]
        public int Id { get; private set; }

        [JsonProperty]
        public string Description { get; set; }

        [JsonProperty]
        public string Pattern { get; set; }

        public IReadOnlyCollection<string> Actions { get; private set; } = emptyActions;

        [JsonProperty("actions")]
        private string RawActions
        {
            set
            {
                Actions = string.IsNullOrEmpty(value)
                    ? emptyActions
                    : (IReadOnlyCollection<string>)new ReadOnlyCollection<string>(value.Split(','));
            }
        }

        [JsonProperty]
        public int Hits { get; private set; }

        [JsonProperty]
        public string Comments { get; set; }

        [JsonProperty]
        public string LastEditor { get; private set; }

        [JsonProperty]
        public DateTime LastEditTime { get; private set; }

        [JsonProperty("deleted")]
        public bool IsDeleted { get; private set; }

        [JsonProperty("private")]
        public bool IsPrivate { get; private set; }

        [JsonProperty("enabled")]
        public bool IsEnabled { get; private set; }

    }
}

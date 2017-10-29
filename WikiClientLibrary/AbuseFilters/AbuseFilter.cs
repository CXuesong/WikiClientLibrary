using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace WikiClientLibrary.AbuseFilters
{
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class AbuseFilter
    {
        private ICollection<string> _Actions;

        [JsonProperty]
        public int Id { get; private set; }

        [JsonProperty]
        public string Description { get; set; }

        [JsonProperty]
        public string Pattern { get; set; }

        public ICollection<string> Actions
        {
            get
            {
                if (_Actions == null) _Actions = new List<string>();
                return _Actions;
            }
            set { _Actions = value; }
        }

        [JsonProperty("actions")]
        private string RawActions
        {
            set { _Actions = string.IsNullOrEmpty(value) ? null : new List<string>(value.Split(',')); }
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
        public bool IsDeleted { get; set; }

        [JsonProperty("private")]
        public bool IsPrivate { get; set; }

        [JsonProperty("enabled")]
        public bool IsEnabled { get; set; }
    }
}

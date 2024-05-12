using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace WikiClientLibrary.AbuseFilters;

[JsonObject(MemberSerialization.OptIn)]
public sealed class AbuseFilter
{

    [JsonProperty]
    public int Id { get; private set; }

    [JsonProperty]
    public string Description { get; set; } = "";

    [JsonProperty]
    public string Pattern { get; set; } = "";

    public IReadOnlyCollection<string> Actions { get; private set; } = Array.Empty<string>();

    [JsonProperty("actions")]
    private string RawActions
    {
        set
        {
            Actions = string.IsNullOrEmpty(value)
                ? Array.Empty<string>()
                : (IReadOnlyCollection<string>)new ReadOnlyCollection<string>(value.Split(','));
        }
    }

    [JsonProperty]
    public int Hits { get; private set; }

    [JsonProperty]
    public string Comments { get; set; } = "";

    [JsonProperty]
    public string LastEditor { get; private set; } = "";

    [JsonProperty]
    public DateTime LastEditTime { get; private set; }

    [JsonProperty("deleted")]
    public bool IsDeleted { get; private set; }

    [JsonProperty("private")]
    public bool IsPrivate { get; private set; }

    [JsonProperty("enabled")]
    public bool IsEnabled { get; private set; }

}
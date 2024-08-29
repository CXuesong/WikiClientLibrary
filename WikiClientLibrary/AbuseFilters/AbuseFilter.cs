using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.AbuseFilters;

[JsonContract]
public sealed class AbuseFilter
{

    public int Id { get; init; }

    public string Description { get; init; } = "";

    public string Pattern { get; init; } = "";

    [JsonIgnore]
    public IReadOnlyCollection<string> Actions { get; init; } = Array.Empty<string>();

    [JsonInclude]
    [JsonPropertyName("actions")]
    private string RawActions
    {
        init
        {
            Actions = string.IsNullOrEmpty(value)
                ? Array.Empty<string>()
                : (IReadOnlyCollection<string>)new ReadOnlyCollection<string>(value.Split(','));
        }
    }

    public int Hits { get; init; }

    public string Comments { get; init; } = "";

    public string? LastEditor { get; init; }

    public DateTime LastEditTime { get; init; }

    [JsonPropertyName("deleted")]
    public bool IsDeleted { get; init; }

    [JsonPropertyName("private")]
    public bool IsPrivate { get; init; }

    [JsonPropertyName("enabled")]
    public bool IsEnabled { get; init; }

}

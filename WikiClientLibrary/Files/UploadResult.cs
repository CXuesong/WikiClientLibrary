using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Files;

/// <summary>
/// Contains the result from server after an upload operation.
/// </summary>
/// <remarks>See https://www.mediawiki.org/wiki/API:Upload .</remarks>
[JsonContract]
public sealed class UploadResult
{

    /// <summary>
    /// A brief word describing the result of the operation.
    /// </summary>
    public UploadResultCode ResultCode { get; init; }

    [JsonInclude]
    [JsonPropertyName("result")]
    private string Result
    {
        init
        {
            ResultCode = value switch
            {
                "Success" => UploadResultCode.Success,
                "Warning" => UploadResultCode.Warning,
                "Continue" => UploadResultCode.Continue,
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown result: " + value)
            };
        }
    }

    /// <summary>
    /// For <see cref="UploadResultCode.Warning"/> and <see cref="UploadResultCode.Continue"/>,
    /// the file key to be passed into the next upload attempt. 
    /// </summary>
    public string? FileKey { get; init; }

    // Same as filekey, maintained for backward compatibility (deprecated in 1.18)
    [JsonInclude]
    private string? SessionKey
    {
        init
        {
            if (FileKey == null) FileKey = value;
            else Debug.Assert(FileKey == value);
        }
    }

    /// <summary>
    /// When performing chunked uploading, gets the starting offset of the next chunk.
    /// </summary>
    public long? Offset { get; init; }

    /// <summary>
    /// Gets a collection of warnings resulted from this upload.
    /// </summary>
    /// <value>
    /// a read-only dictionary of warning code - context pairs.
    /// The list is guaranteed not to be <c>null</c>,
    /// but it can be empty.
    /// </value>
    /// <remarks>
    /// <para>You can use <see cref="UploadWarningCollection.FormatWarning"/> to get user-friendly warning messages.</para>
    /// <para>If you have suppressed warnings, the warnings will still be here, but <see cref="ResultCode"/> will be <see cref="UploadResultCode.Success"/>.</para>
    /// </remarks>
    public UploadWarningCollection Warnings { get; init; } = UploadWarningCollection.Empty;

    /// <summary>
    /// Gets a collection of errors during stashing the chunk or the file to be uploaded.
    /// (MW 1.29+)
    /// </summary>
    public IReadOnlyList<StashError> StashErrors { get; init; } = ImmutableList<StashError>.Empty;

    /// <summary>
    /// For a successful upload or stashing, gets the revision information
    /// for the uploaded file.
    /// </summary>
    [JsonPropertyName("imageinfo")]
    public FileRevision? FileRevision { get; init; }

    /// <summary>
    /// 返回表示当前对象的字符串。
    /// </summary>
    /// <returns>
    /// 表示当前对象的字符串。
    /// </returns>
    public override string ToString()
    {
        return $"{ResultCode}; {string.Join(",", Warnings.Select(p => p.Key))}";
    }

}

/// <summary>
/// General results of an upload operation.
/// </summary>
public enum UploadResultCode
{

    Success = 0,
    Warning,
    Continue

}

/// <summary>
/// A collection containing the warning messages of file upload.
/// </summary>
public class UploadWarningCollection : WikiReadOnlyDictionary
{

    internal static readonly UploadWarningCollection Empty = new();

    private static readonly Dictionary<string, string> warningMessages = new Dictionary<string, string>
    {
        // Referenced from pywikibot, site.py
        // map API warning codes to user error messages
        // {0} will be replaced by message string from API response
        { "duplicate-archive", "The file is a duplicate of a deleted file {0}." },
        { "was-deleted", "The file {0} was previously deleted." },
        { "emptyfile", "File {0} is empty." },
        { "exists", "File {0} already exists." },
        { "duplicate", "Uploaded file is a duplicate of {0}." },
        { "duplicateversions", "Uploaded file is a duplicate of previous version(s): {0}." },
        { "badfilename", "Target filename is invalid. Suggested filename is {0}." },
        { "filetype-unwanted-type", "File {0} type is unwanted type." },
        { "exists-normalized", "File exists with different extension as \"{0}\"." },
    };

    /// <summary>
    /// The file content is empty. (<c>emptyfile</c>)
    /// </summary>
    public bool IsEmptyFile => GetBooleanValue("emptyfile");

    /// <summary>
    /// A file with the same title already exists. (<c>exists</c>)
    /// </summary>
    public bool TitleExists => GetBooleanValue("exists");

    /// <summary>
    /// File exists with different extension as the value of this property. (<c>exists-normalized</c>)
    /// </summary>
    /// <value><c>null</c> if there is no such warning in the response.</value>
    public string? ExistingAlternativeExtension => GetStringValue("exists-normalized");

    /// <summary>
    /// Target filename is invalid. (<c>badfilename</c>)
    /// </summary>
    public bool IsBadFileName => GetBooleanValue("badfilename");

    /// <summary>
    /// The file type is of an unwanted type.
    /// </summary>
    public bool IsUnwantedType => GetBooleanValue("filetype-unwanted-type");

    /// <summary>
    /// The file with the specified title was previously deleted. (<c>was-deleted</c>)
    /// </summary>
    public bool WasTitleDeleted => GetBooleanValue("was-deleted");

    /// <summary>
    /// The file content is a duplicate of a deleted file. (<c>duplicate-archive</c>)
    /// </summary>
    public bool WasContentDeleted => GetBooleanValue("duplicate-archive");

    /// <inheritdoc />
    protected override void OnDeserialized()
    {
        if (TryGetValue("duplicateversions", out var jversions)
            && jversions.ValueKind == JsonValueKind.Array
            && jversions.GetArrayLength() > 0)
        {
            var versions = jversions.EnumerateArray()
                .Select(e => MediaWikiHelper.ParseDateTime(e.GetProperty("timestamp").GetString()))
                .ToList();
            DuplicateVersions = new ReadOnlyCollection<DateTime>(versions);
        }
        else
        {
            DuplicateVersions = null;
        }
        if (TryGetValue("duplicate", out var jduplicates)
            && jduplicates.ValueKind == JsonValueKind.Array
            && jduplicates.GetArrayLength() > 0)
        {
            var titles = jduplicates.EnumerateArray().Select(t => t.GetString()).ToList();
            DuplicateTitles = new ReadOnlyCollection<string>(titles);
        }
        else
        {
            DuplicateTitles = null;
        }
    }

    /// <summary>
    /// The uploaded file has duplicate content to these titles. (<c>duplicate</c>)
    /// </summary>
    /// <value><c>null</c> if there is no such warning in the response.</value>
    public IList<string>? DuplicateTitles { get; private set; }

    /// <summary>
    /// Uploaded file is duplicate of these versions. (<c>duplicateversions</c>)
    /// </summary>
    /// <value><c>null</c> if there is no such warning in the response.</value>
    public IList<DateTime>? DuplicateVersions { get; private set; }

    /// <summary>
    /// Try to convert the specified warning code and context into a user-friendly
    /// warning message.
    /// </summary>
    /// <param name="warningCode">Case-sensitive warning code.</param>
    /// <param name="context">The extra content of the warning.</param>
    /// <returns>
    /// It tries to match the warningCode with well-known ones, and returns a
    /// user-friendly warning message. If there's no match, a string containing
    /// warningCode and context will be returned.
    /// </returns>
    public static string FormatWarning(string warningCode, JsonElement context)
    {
        string? contextString = null;
        if (context.ValueKind != JsonValueKind.Undefined)
        {
            switch (warningCode)
            {
                case "duplicateversions":
                    var timeStamps = context.EnumerateArray()
                        .Select(v => MediaWikiHelper.ParseDateTime(v.GetProperty("timestamp").GetString()))
                        .Take(4).ToList();
                    contextString = string.Join(",", timeStamps.Take(3));
                    if (timeStamps.Count > 3) contextString += ",…";
                    break;
                case "duplicate":
                    var titles = context.EnumerateArray()
                        .Select(v => v.GetString())
                        .Take(4).ToList();
                    contextString = string.Join(",", titles.Take(3));
                    if (titles.Count > 3) contextString += ",…";
                    break;
                default:
                    contextString = context.ToString();
                    break;
            }
        }
        if (warningMessages.TryGetValue(warningCode, out var template))
            return string.Format(template, contextString);
        return $"{warningCode}: {contextString}";
    }

    /// <summary>
    /// Gets the formatted warning messages, one warning per line.
    /// </summary>
    public override string ToString()
    {
        return string.Join("\n", this.Select(p => FormatWarning(p.Key, p.Value)));
    }

}

/// <summary>
/// Represents an stash error entry in the MediaWiki file upload result.
/// </summary>
[JsonContract]
public class StashError
{

    /// <summary>Error code.</summary>
    public string Code { get; init; } = "";

    /// <summary>Error message.</summary>
    public string Message { get; init; } = "";

    /// <summary>Additional error details.</summary>
    public IReadOnlyList<string> Params { get; init; } = ImmutableList<string>.Empty;

    /// <summary>Error type. The value is usually one of <c>"error"</c> or <c>"warning"</c>.</summary>
    public string Type { get; init; } = "";

    /// <inheritdoc />
    public override string ToString() => $"{Code}: {Message}";

}

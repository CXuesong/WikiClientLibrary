using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Pages.Queries.Properties;

namespace WikiClientLibrary.Files
{

    /// <summary>
    /// Represents a revision of a file or image.
    /// </summary>
    /// <seealso cref="FileInfoPropertyGroup"/>
    /// <seealso cref="FileInfoPropertyProvider"/>
    [JsonObject(MemberSerialization.OptIn)]
    public class FileRevision
    {

        /// <summary>
        /// Gets the stub of page this revision applies to.
        /// </summary>
        public WikiPageStub Page { get; internal set; }

        [JsonProperty("extmetadata")]
        public Dictionary<string, MetadataValue> ExtMetadata { get; set; }

        /// <summary>
        /// Whether the file is anonymous. (MW ~1.33-)
        /// </summary>
        /// <remarks>
        /// Before ~MW 1.33, if this revision indicates a stashed file, this property will be <c>true</c>.
        /// </remarks>
        [JsonProperty("anon")]
        public bool IsAnonymous { get; private set; }

        [JsonProperty]
        public int BitDepth { get; private set; }

        /// <summary>
        /// MIME type of the file.
        /// </summary>
        [JsonProperty]
        public string Mime { get; private set; }

        /// <summary>
        /// The time and date of the revision.
        /// </summary>
        [JsonProperty]
        public DateTime TimeStamp { get; private set; }

        /// <summary>
        /// Name of the user uploading this file revision.
        /// </summary>
        [JsonProperty("user")]
        public string UserName { get; private set; }

        /// <summary>
        /// The comment associated with the upload of this revision.
        /// </summary>
        [JsonProperty]
        public string Comment { get; private set; }

        /// <summary>
        /// Url of the file.
        /// </summary>
        [JsonProperty]
        public string Url { get; private set; }

        /// <summary>
        /// Url of the description page.
        /// </summary>
        [JsonProperty]
        public string DescriptionUrl { get; private set; }

        /// <summary>
        /// Size of the file. In bytes.
        /// </summary>
        [JsonProperty]
        public int Size { get; private set; }

        [JsonProperty]
        public int Width { get; private set; }

        [JsonProperty]
        public int Height { get; private set; }

        /// <summary>
        /// The file's SHA-1 hash.
        /// </summary>
        [JsonProperty]
        public string Sha1 { get; private set; }
    }

    /// <summary>
    /// Contains the result from server after an upload operation.
    /// </summary>
    /// <remarks>See https://www.mediawiki.org/wiki/API:Upload .</remarks>
    [JsonObject(MemberSerialization.OptIn)]
    public class UploadResult
    {

        /// <summary>
        /// A brief word describing the result of the operation.
        /// </summary>
        public UploadResultCode ResultCode { get; private set; }

        [JsonProperty("result")]
        private string Result
        {
            set
            {
                switch (value)
                {
                    case "Success":
                        ResultCode = UploadResultCode.Success;
                        break;
                    case "Warning":
                        ResultCode = UploadResultCode.Warning;
                        break;
                    case "Continue":
                        ResultCode = UploadResultCode.Continue;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown result: " + value);
                }
            }
        }

        /// <summary>
        /// For <see cref="UploadResultCode.Warning"/> and <see cref="UploadResultCode.Continue"/>,
        /// the file key to be passed into the next upload attempt. 
        /// </summary>
        [JsonProperty]
        public string FileKey { get; private set; }

        // Same as filekey, maintained for backward compatibility (deprecated in 1.18)
        [JsonProperty]
        private string SessionKey
        {
            set
            {
                if (FileKey == null) FileKey = value;
                else Debug.Assert(FileKey == value);
            }
        }

        /// <summary>
        /// When performing chunked uploading, gets the starting offset of the next chunk.
        /// </summary>
        [JsonProperty]
        public long? Offset { get; private set; }

        /// <summary>
        /// Gets a collection of warnings resulted from this upload.
        /// </summary>
        /// <value>
        /// A read-only dictionary of warning code - context pairs.
        /// The list is guaranteed not to be <c>null</c>,
        /// but it can be empty.
        /// </value>
        /// <remarks>
        /// <para>You can use <see cref="UploadWarningCollection.FormatWarning"/> to get user-friendly warning messages.</para>
        /// <para>If you have suppressed warnings, the warnings will still be here, but <see cref="ResultCode"/> will be <see cref="UploadResultCode.Success"/>.</para>
        /// </remarks>
        [JsonProperty("warnings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public UploadWarningCollection Warnings { get; private set; } = UploadWarningCollection.Empty;

        /// <summary>
        /// For a successful upload or stashing, gets the revision information
        /// for the uploaded file.
        /// </summary>
        [JsonProperty("imageinfo")]
        public FileRevision FileRevision { get; private set; }

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

        internal static readonly UploadWarningCollection Empty = new UploadWarningCollection();

        private static readonly Dictionary<string, string> warningMessages = new Dictionary<string, string>
        {
            // Referenced from pywikibot, site.py
            // map API warning codes to user error messages
            // {0} will be replaced by message string from API response
            {"duplicate-archive", "The file is a duplicate of a deleted file {0}."},
            {"was-deleted", "The file {0} was previously deleted."},
            {"emptyfile", "File {0} is empty."},
            {"exists", "File {0} already exists."},
            {"duplicate", "Uploaded file is a duplicate of {0}."},
            {"duplicateversions", "Uploaded file is a duplicate of previous version(s): {0}."},
            {"badfilename", "Target filename is invalid. Suggested filename is {0}."},
            {"filetype-unwanted-type", "File {0} type is unwanted type."},
            {"exists-normalized", "File exists with different extension as \"{0}\"."},
        };

        static UploadWarningCollection()
        {
            Empty.MakeReadonly();
        }

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
        public string ExistingAlternativeExtension => GetStringValue("exists-normalized");

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

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (GetValueDirect("duplicateversions") is JArray jversions && jversions.Count > 0)
            {
                var versions = jversions.Select(v => MediaWikiHelper.ParseDateTime((string)v["timestamp"])).ToList();
                DuplicateVersions = new ReadOnlyCollection<DateTime>(versions);
            }
            else
            {
                DuplicateVersions = null;
            }
            if (GetValueDirect("duplicate") is JArray jdumplicates && jdumplicates.Count > 0)
            {
                var titles = jdumplicates.Select(t => (string)t).ToList();
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
        public IList<string> DuplicateTitles { get; private set; }

        /// <summary>
        /// Uploaded file is duplicate of these versions. (<c>duplicateversions</c>)
        /// </summary>
        /// <value><c>null</c> if there is no such warning in the response.</value>
        public IList<DateTime> DuplicateVersions { get; private set; }

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
        public static string FormatWarning(string warningCode, JToken context)
        {
            string contextString = null;
            if (context != null)
            {
                switch (warningCode)
                {
                    case "duplicateversions":
                        var timeStamps = context.Select(v => (DateTime)v["timestamp"]).Take(4).ToArray();
                        contextString = string.Join(",", timeStamps.Take(3));
                        if (timeStamps.Length > 3) contextString += ",…";
                        break;
                    case "duplicate":
                        var titles = context.Select(v => (string)v).Take(4).ToArray();
                        contextString = string.Join(",", titles.Take(3));
                        if (titles.Length > 3) contextString += ",…";
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

    [JsonObject(MemberSerialization.OptIn)]
    public class MetadataValue
    {
        [JsonProperty("value")]
        public object Value { get; set; }
        [JsonProperty("source")]
        public string Source { get; set; }
        [JsonProperty("hidden")]
        public string Hidden { get; set; }
    }
}

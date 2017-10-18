using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Client
{
    /// <summary>
    /// The MediaWiki API request message consisting of parameter key-value pairs (fields).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the primary message type used in the WikiClientLibrary.
    /// This type provides some useful functionalities such as constructing fields from anonymous objects,
    /// and simple type (e.g. <see cref="DateTime"/>) marshalling.
    /// while overriding the rest.
    /// </para>
    /// <para>
    /// This message can be later converted into
    /// <c>application/x-www-form-urlencoded</c> or <c>multipart/form-data</c> http content.
    /// </para>
    /// <para>
    /// When converting the form into <see cref="HttpContent"/>, the values are marshaled in the following way
    /// <list type="bullet">
    /// <item><description>
    /// <c>null</c> values are ignored. 
    /// </description></item>
    /// <item><description>
    /// <c>string</c> values are kept intact. 
    /// </description></item>
    /// <item><description>
    /// <c>bool</c> values are marshaled as <c>""</c>(<see cref="string.Empty"/>) for <c>true</c>,
    /// and are ignored for <c>false</c>.
    /// </description></item>
    /// <item><description>
    /// <see cref="DateTime"/> values are marshaled as UTC in ISO 8601 format.
    /// </description></item>
    /// <item><description>
    /// <see cref="Stream"/> values are sent as <see cref="StreamContent"/> with a dummy file name,
    /// and this will force the whole form to be marshaled as <see cref="MultipartFormDataContent"/>.
    /// </description></item>
    /// <item><description>
    /// <see cref="AutoWatchBehavior"/> values are marshaled as one of "preferences", "nochange", "watch", "unwatch".
    /// </description></item>
    /// <item><description>
    /// Other types of values are marshaled by calling <see cref="object.ToString"/> on them.
    /// </description></item>
    /// </list>
    /// Note that the message sending methods (e.g. <see cref="WikiSite.GetJsonAsync(WikiRequestMessage,CancellationToken)"/>)
    /// may also change the way the message is marshaled. For the detailed information, please see the message sender's
    /// documentations respectively.
    /// </para>
    /// <para>This message uses POST method to invoke API requests.</para>
    /// </remarks>
    public class MediaWikiFormRequestMessage : WikiRequestMessage
    {

        private readonly IList<KeyValuePair<string, object>> fields;
        private IList<KeyValuePair<string, object>> readonlyFields;
        // Memorizes the stream position upon first request.
        private IDictionary<Stream, long> streamPositions;

        private volatile int status;
        // Created
        private const int STATUS_CREATED = 0;
        // GetHttpContent invoked, recoverable
        private const int STATUS_RECOVERABLE = 1;
        // GetHttpContent invoked, un-recoverable
        private const int STATUS_UNRECOVERABLE = 2;

        /// <inheritdoc cref="MediaWikiFormRequestMessage(string,object,bool)"/>
        public MediaWikiFormRequestMessage(object fieldCollection) : this(null, fieldCollection, false)
        {
        }

        /// <inheritdoc cref="MediaWikiFormRequestMessage(string,object,bool)"/>
        public MediaWikiFormRequestMessage(object fieldCollection, bool forceMultipartFormData) : this(null, fieldCollection, forceMultipartFormData)
        {
        }

        /// <inheritdoc />
        /// <param name="fieldCollection">A dictionary or anonymous object containing the key-value pairs. See <see cref="MediaWikiHelper.EnumValues"/> for more information.</param>
        /// <param name="forceMultipartFormData">Forces the message to be marshaled as multipart/form-data, regardless of the fields.</param>
        /// <exception cref="ArgumentNullException"><paramref name="fieldCollection"/> is <c>null</c>.</exception>
        public MediaWikiFormRequestMessage(string id, object fieldCollection, bool forceMultipartFormData) : base(id)
        {
            if (fieldCollection == null) throw new ArgumentNullException(nameof(fieldCollection));
            fields = new List<KeyValuePair<string, object>>(MediaWikiHelper.EnumValues(fieldCollection));
            AsMultipartFormData = forceMultipartFormData || fields.Any(p => p.Value is Stream);
        }

        /// <summary>
        /// Determines whether this message should be marshaled in <c>multipart/form-data</c> MIME type.
        /// </summary>
        public bool AsMultipartFormData { get; }

        /// <summary>
        /// Gets a readonly list of all the fields in the form.
        /// </summary>
        public IList<KeyValuePair<string, object>> Fields
        {
            get
            {
                if (readonlyFields != null) return readonlyFields;
                var local = new ReadOnlyCollection<KeyValuePair<string, object>>(fields);
                Volatile.Write(ref readonlyFields, local);
                return readonlyFields;
            }
        }

        /// <inheritdoc />
        public override HttpMethod GetHttpMethod() => HttpMethod.Post;

        /// <inheritdoc />
        public override string GetHttpQuery() => null;

        /// <inheritdoc />
        public override HttpContent GetHttpContent()
        {
            // Save & restore the stream position on each GetHttpContent call.
            switch (status)
            {
                case STATUS_CREATED:
                    IDictionary<Stream, long> sps = null;
                    foreach (var p in fields)
                    {
                        if (p.Value is Stream s)
                        {
                            if (s.CanSeek)
                            {
                                if (sps == null) sps = new Dictionary<Stream, long>();
                                sps[s] = s.Position;
                            }
                            else
                            {
                                status = STATUS_UNRECOVERABLE;
                                goto MAIN;
                            }
                        }
                    }
                    streamPositions = sps;
                    status = STATUS_RECOVERABLE;
                    break;
                case STATUS_RECOVERABLE:
                    sps = streamPositions;
                    if (sps != null)
                    {
                        foreach (var p in fields)
                        {
                            if (p.Value is Stream s) s.Position = sps[s];
                        }
                    }
                    break;
                case STATUS_UNRECOVERABLE:
                    throw new InvalidOperationException("Cannot recover the field state (e.g. Stream position).");
            }
            MAIN:
            if (AsMultipartFormData)
            {
                var content = new MultipartFormDataContent();
                foreach (var p in fields)
                {
                    switch (p.Value)
                    {
                        case string s:
                            content.Add(new StringContent(s), p.Key);
                            break;
                        case Stream stream:
                            content.Add(new KeepAlivingStreamContent(stream), p.Key, "dummy");
                            break;
                        case null:
                            // Ignore null entries.
                            break;
                        default:
                            var stringValue = Utility.ToWikiQueryValue(p.Value);
                            if (stringValue != null)
                                content.Add(new StringContent(stringValue), p.Key);
                            break;
                    }
                }
                return content;
            }
            else
            {
                return new FormLongUrlEncodedContent(Utility.ToWikiStringValuePairs(fields)
                    .Where(p => p.Value != null));
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Client
{

    /// <summary>
    /// The traceable MediaWiki API request message.
    /// </summary>
    public abstract class WikiRequestMessage
    {

        private static readonly long baseCounter =
            (long) (Environment.TickCount ^ RuntimeInformation.OSDescription.GetHashCode()) << 32;

        private static int idCounter;

        public WikiRequestMessage(string id)
        {
            Id = id ?? NextId();
        }

        private static string NextId()
        {
            var localCounter = Interlocked.Increment(ref idCounter);
            return (baseCounter | (uint) localCounter).ToString("X16");
        }

        /// <summary>
        /// Id of the request. For tracing.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Throws various exceptions on detecting the "error" nodes in the response.
        /// </summary>
        public virtual void ValidateResponse(JToken response, ILogger logger)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            if (!(response is JObject)) return;
            // See https://www.mediawiki.org/wiki/API:Errors_and_warnings .
            /*
    "warnings": {
        "main": {
            "*": "xxxx"
        },
        "login": {
            "*": "xxxx"
        }
    }
             */
            if (response["warnings"] != null && logger.IsEnabled(LogLevel.Warning))
            {
                foreach (var module in ((JObject) response["warnings"]).Properties())
                {
                    logger.LogWarning("Ignored warning for {Request}: {Module}: {Warning}", this, module.Name,
                        module.Value);
                }
            }
            var err = response["error"];
            if (err != null)
            {
                var errcode = (string) err["code"];
                // err["*"]: API usage.
                var errmessage = ((string) err["info"]).Trim();
                logger.LogWarning("Dispatch error for {Request}: {Code} - {Message}", this, errcode, errmessage);
                switch (errcode)
                {
                    case "permissiondenied":
                    case "readapidenied": // You need read permission to use this module
                    case "mustbeloggedin": // You must be logged in to upload this file.
                        throw new UnauthorizedOperationException(errcode, errmessage);
                    case "badtoken":
                        throw new BadTokenException(errcode, errmessage);
                    case "unknown_action":
                        throw new InvalidActionException(errcode, errmessage);
                    case "assertuserfailed":
                    case "assertbotfailed":
                        throw new AccountAssertionFailureException(errcode, errmessage);
                    default:
                        if (errcode.EndsWith("conflict"))
                            throw new OperationConflictException(errcode, errmessage);
                        throw new OperationFailedException(errcode, errmessage);
                }
            }
        }

        /// <summary>
        /// Gets the <see cref="HttpContent"/> corresponding to this message.
        /// </summary>
        public abstract HttpContent GetHttpContent();

        /// <inheritdoc />
        public override string ToString()
        {
            return Id;
        }
    }

    /// <summary>
    /// The MediaWiki API request message consisting of parameter key-value pairs (fields).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the primary message type used in the WikiClientLibrary.
    /// This type provides some useful functionalities such as constructing fields from anonymous objects,
    /// simple type marshalling, or inherit some of the fields of other <see cref="WikiFormRequestMessage"/> instance,
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
    /// </remarks>
    public class WikiFormRequestMessage : WikiRequestMessage
    {

        private readonly WikiFormRequestMessage baseForm;
        private readonly IDictionary<string, object> fieldDict;
        private IDictionary<string, object> readonlyFieldDict;
        // Memorizes the stream position upon first request.
        private IDictionary<Stream, long> streamPositions;

        private volatile int status;
        // Created
        private const int STATUS_CREATED = 0;
        // GetHttpContent invoked, recoverable
        private const int STATUS_RECOVERABLE = 1;
        // GetHttpContent invoked, un-recoverable
        private const int STATUS_UNRECOVERABLE = 2;

        private bool hasUnrecoverableFields = false;

        public WikiFormRequestMessage(object fieldCollection) : this(null, null, fieldCollection, false)
        {
        }

        public WikiFormRequestMessage(object fieldCollection, bool forceMultipartFormData) : this(null, null,
            fieldCollection, forceMultipartFormData)
        {
        }

        public WikiFormRequestMessage(string id, object fieldCollection) : this(id, null, fieldCollection, false)
        {
        }

        public WikiFormRequestMessage(string id, WikiFormRequestMessage baseForm,
            object fieldCollection, bool forceMultipartFormData) : base(id)
        {
            if (baseForm == null)
                fieldDict = new Dictionary<string, object>();
            else
                fieldDict = new Dictionary<string, object>(baseForm.fieldDict);
            // Override values.
            foreach (var p in Utility.EnumValues(fieldCollection))
                fieldDict[p.Key] = p.Value;
            if (forceMultipartFormData || (baseForm?.AsMultipartFormData ?? false)) AsMultipartFormData = true;
            else AsMultipartFormData = this.fieldDict.Any(p => p.Value is Stream);
            this.baseForm = baseForm;
        }

        /// <summary>
        /// Determines whether this message should be marshaled in <c>multipart/form-data</c> MIME type.
        /// </summary>
        public bool AsMultipartFormData { get; }

        /// <summary>
        /// Gets a readonly dictionary of all the fields in the form.
        /// </summary>
        public IDictionary<string, object> Fields
        {
            get
            {
                if (readonlyFieldDict != null) return readonlyFieldDict;
                var local = new ReadOnlyDictionary<string, object>(fieldDict);
                Volatile.Write(ref readonlyFieldDict, local);
                return local;
            }
        }

        /// <inheritdoc />
        public override void ValidateResponse(JToken response, ILogger logger)
        {
            if (baseForm != null) baseForm.ValidateResponse(response, logger);
            else base.ValidateResponse(response, logger);
        }

        /// <inheritdoc />
        public override HttpContent GetHttpContent()
        {
            // Save & restore the stream position on each GetHttpContent call.
            switch (status)
            {
                case STATUS_CREATED:
                    IDictionary<Stream, long> sps = null;
                    foreach (var p in fieldDict)
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
                        foreach (var p in fieldDict)
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
                foreach (var p in fieldDict)
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
                return new FormLongUrlEncodedContent(Utility.ToWikiStringValuePairs(fieldDict)
                    .Select(p => new KeyValuePair<string, string>(p.Key, Utility.ToWikiQueryValue(p.Value)))
                    .Where(p => p.Value != null));
            }
        }
    }

    /// <summary>
    /// Directly encapsulates a <see cref="HttpContent"/> into <see cref="WikiRequestMessage"/>.
    /// </summary>
    /// <remarks>
    /// Use <see cref="WikiFormRequestMessage"/> wherever possible,
    /// Because various enhancements are not available on this type.
    /// </remarks>
    public sealed class WikiRawRequestMessage : WikiRequestMessage
    {

        private readonly HttpContent httpContent;

        /// <inheritdoc />
        public WikiRawRequestMessage(string id, HttpContent httpContent) : base(id)
        {
            this.httpContent = httpContent ?? throw new ArgumentNullException(nameof(httpContent));
        }

        /// <inheritdoc />
        public override HttpContent GetHttpContent()
        {
            // appendedValues will be silently ignored.
            return httpContent;
        }
    }

}

using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Pages;

namespace WikiClientLibrary.Wikibase
{
    /// <summary>
    /// Atomic instances indicating a Wikibase Property type.
    /// </summary>
    public abstract class PropertyType
    {
        public abstract string Name { get; }

        public abstract object Parse(JToken expr);

        public abstract JToken ToJson(object value);
    }

    internal class DelegatePropertyType<T> : PropertyType
    {

        private readonly Func<JToken, T> parseHandler;
        private readonly Func<T, JToken> toJsonHandler;

        public DelegatePropertyType(string name, Func<JToken, T> parseHandler, Func<T, JToken> toJsonHandler)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            this.parseHandler = parseHandler ?? throw new ArgumentNullException(nameof(parseHandler));
            this.toJsonHandler = toJsonHandler ?? throw new ArgumentNullException(nameof(toJsonHandler));
        }

        public override string Name { get; }

        public override object Parse(JToken expr)
        {
            return parseHandler(expr);
        }

        public override JToken ToJson(object value)
        {
            if (value == null) return null;
            if (value is T t)
                return toJsonHandler(t);
            throw new ArgumentException("Value type is incompatible.", nameof(value));
        }

    }

    public static class PropertyTypes
    {

        public static PropertyType WikibaseItem { get; } = new DelegatePropertyType<string>("wikibase-item",
            e => (string) e, v => v);

        public static PropertyType String { get; } = new DelegatePropertyType<string>("string",
            e => (string) e, v => v);

        public static PropertyType CommonsMedia { get; } = new DelegatePropertyType<string>("commonsMedia",
            e => (string) e, v => v);

        public static PropertyType Url { get; } = new DelegatePropertyType<string>("url",
            e => (string) e, v => v);

        public static PropertyType Time { get; } = new DelegatePropertyType<WikibaseTime>("time",
            e =>
            {
                var time = (string) e["time"];
                var timeZone = (int) e["timezone"];
                var before = (int) e["before"];
                var after = (int) e["after"];
                var precision = (WikibaseTimePrecision) (int) e["precision"];
                var calendar = (string) e["calendarmodel"];
                return new WikibaseTime(time, before, after, timeZone, precision, WikibaseUri.Get(calendar));
            }, v =>
            {
                var obj = new JObject
                {
                    {"time", v.ToIso8601UtcString()},
                    {"timezone", v.TimeZone},
                    {"before", v.Before},
                    {"after", v.After},
                    {"precision", (int) v.Precision},
                    {"calendarmodel", v.CalendarModel.Uri}
                };
                return obj;
            });

    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using WikiClientLibrary.Infrastructures;

namespace WikiClientLibrary.Flow
{
    internal static class FlowUtility
    {

        public static readonly JsonSerializer FlowJsonSerializer =
            JsonSerializer.CreateDefault(new JsonSerializerSettings
            {
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                NullValueHandling = NullValueHandling.Include,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),    // Flow uses camel-case.
                Converters =
                {
                    new WikiStringEnumJsonConverter(),
                    new FlowUserStubConverter(),
                },
            });
        
        public static bool IsNullOrJsonNull(JToken token)
        {
            return token == null || token.Type == JTokenType.Null;
        }

        public static UserStub UserFromJson(JToken user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            Gender gender;
            switch ((string)user["gender"])
            {
                case "male":
                    gender = Gender.Male;
                    break;
                case "female":
                    gender = Gender.Female;
                    break;
                default:
                    gender = Gender.Unknown;
                    break;
            }
            return new UserStub((string)user["name"], (int?)user["id"], gender, (string)user["wiki"]);
        }

        public static DateTime DateFromJavaScriptTicks(long ticks)
        {
            // 621355968000000000: 1970-01-01
            return new DateTime(621355968000000000L + ticks * 10000L, DateTimeKind.Utc);
        }

    }

    internal class FlowUserStubConverter : JsonConverter
    {

        private static readonly object boxedEmptyUserStub = UserStub.Empty;

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return boxedEmptyUserStub;
            if (reader.TokenType != JsonToken.StartObject) throw new JsonSerializationException("Expect JSON object.");
            var jUser = JToken.ReadFrom(reader);
            if (FlowUtility.IsNullOrJsonNull(jUser["name"]))
            {
                Debug.Assert(FlowUtility.IsNullOrJsonNull(jUser["id"]));
                // jUser["gender"] == "unknown"
                Debug.Assert(FlowUtility.IsNullOrJsonNull(jUser["site"]));
                return boxedEmptyUserStub;
            }
            return FlowUtility.UserFromJson(jUser);
        }

        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(UserStub);
        }

    }
    
}

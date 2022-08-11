// <auto-generated />
//
// To parse this JSON data, add NuGet 'Newtonsoft.Json' then do:
//
//    using BasicBot.GraphQL.EventId;
//
//    var eventId = EventId.FromJson(jsonString);

namespace BasicBot.GraphQL.EventId
{
    using System;
    using System.Collections.Generic;

    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public partial class EventId
    {
        [JsonProperty("data")]
        public Data Data { get; set; }

        [JsonProperty("extensions")]
        public Extensions Extensions { get; set; }

        [JsonProperty("actionRecords")]
        public List<object> ActionRecords { get; set; }
    }

    public partial class Data
    {
        [JsonProperty("event")]
        public Event Event { get; set; }
    }

    public partial class Event
    {
        [JsonProperty("id")]
        [JsonConverter(typeof(ParseStringConverter))]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name;
    }

    public partial class Extensions
    {
        [JsonProperty("cacheControl")]
        public CacheControl CacheControl { get; set; }

        [JsonProperty("queryComplexity")]
        public long QueryComplexity { get; set; }
    }

    public partial class CacheControl
    {
        [JsonProperty("version")]
        public long Version { get; set; }

        [JsonProperty("hints")]
        public List<Hint> Hints { get; set; }
    }

    public partial class Hint
    {
        [JsonProperty("path")]
        public List<string> Path { get; set; }

        [JsonProperty("maxAge")]
        public long MaxAge { get; set; }

        [JsonProperty("scope")]
        public string Scope { get; set; }
    }

    public partial class EventId
    {
        public static EventId FromJson(string json) => JsonConvert.DeserializeObject<EventId>(json, BasicBot.GraphQL.EventId.Converter.Settings);
    }

    public static class Serialize
    {
        public static string ToJson(this EventId self) => JsonConvert.SerializeObject(self, BasicBot.GraphQL.EventId.Converter.Settings);
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }
    
    internal class ParseStringConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(string) || t == typeof(string);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            return value;
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            serializer.Serialize(writer, untypedValue.ToString());
            return;
        }

        public static readonly ParseStringConverter Singleton = new ParseStringConverter();
    }
}
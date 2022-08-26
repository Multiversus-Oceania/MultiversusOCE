using System.Collections.Generic;
using Newtonsoft.Json;

namespace BasicBot.Settings
{
    public class Bot
    {
        [JsonProperty]
        public string BotToken { get; internal set; }

        [JsonProperty]
        public string BotPrefix { get; internal set; }

        [JsonProperty]
        public List<ulong> BotOwners { get; internal set; }

        [JsonProperty]
        public string StartGGToken { get; internal set; }

        [JsonProperty]
        public string MongoConnection { get; internal set; }

        [JsonProperty]
        public string MongoDatabase { get; internal set; }
    }
}
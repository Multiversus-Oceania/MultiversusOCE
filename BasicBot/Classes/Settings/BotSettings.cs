using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

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
    }
}

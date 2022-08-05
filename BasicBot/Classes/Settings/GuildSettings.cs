using System.Collections.Generic;
using Newtonsoft.Json;

namespace BasicBot.Settings
{
    public class Guild
    {
        [JsonProperty]
        public ulong guildId;

        [JsonProperty]
        public StaffRoles StaffRole = new();

        [JsonIgnore]
        public int? ModerationCount = null;

        [JsonProperty]
        public Dictionary<string, List<string>> Maps = new();

        [JsonProperty]
        public ulong TournamentCategory;

        public class StaffRoles
        {
            [JsonProperty]
            public List<ulong> Admin = new();

            [JsonProperty]
            public List<ulong> Management = new();

            [JsonProperty]
            public List<ulong> Support = new();
        }
    }
}
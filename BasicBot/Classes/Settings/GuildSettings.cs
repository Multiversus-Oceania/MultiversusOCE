using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace BasicBot.Settings
{
    public class Guild
    {
        public enum ChannelType
        {
            Log,
            Ping,
            TournamentBoard
        }

        [BsonId]
        public ObjectId mongoId;

        [JsonProperty]
        public ulong guildId;

        [JsonProperty]
        public StaffRoles StaffRole = new();

        [JsonProperty]
        public Dictionary<string, List<string>> Maps = new();

        [JsonProperty]
        public ulong TournamentCategory;

        [BsonRepresentation(BsonType.Array)]
        public Dictionary<ChannelType, ulong> Channels = new();

        public List<ulong> ModPingRoles = new();

        public List<ulong> GameRoomRoles = new();

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
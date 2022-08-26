using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using MongoDB.Driver;
using static BasicBot.Handler.String;

namespace BasicBot.Handler
{
    public static class Guild
    {
        private static Dictionary<ulong, BasicBot.Settings.Guild> Guilds = null;

        private static readonly string GuildsFile = CombineCurrentDirectory("Guilds.json");

        public static MongoClient Client = new(Settings.GetSettings().MongoConnection);
        public static IMongoDatabase Database = Client.GetDatabase(Settings.GetSettings().MongoDatabase);

        public static IMongoCollection<BasicBot.Settings.Guild> GuildCollection =
            Database.GetCollection<BasicBot.Settings.Guild>("guilds");

        #region user settings

        private static void Setup()
        {
            Guilds = new Dictionary<ulong, BasicBot.Settings.Guild>();

            Client = new MongoClient(Settings.GetSettings().MongoConnection);
            Database = Client.GetDatabase(Settings.GetSettings().MongoDatabase);

            // BsonClassMap.RegisterClassMap<BasicBot.Settings.Guild>();

            GuildCollection = Database.GetCollection<BasicBot.Settings.Guild>("guilds");
        }

        public static async void SaveGuild(BasicBot.Settings.Guild guild)
        {
            Console.WriteLine(guild.guildId);
            var filter = Builders<BasicBot.Settings.Guild>.Filter.Eq("guildId", guild.guildId);
            await GuildCollection.ReplaceOneAsync(filter, guild, new ReplaceOptions()
            {
                IsUpsert = true
            });
        }

        public static void SaveGuilds()
        {
            if (Guilds != null)
            {
                List<Task> tasks = new List<Task>();
                foreach (var guild in Guilds)
                {
                    var filter = Builders<BasicBot.Settings.Guild>.Filter.Eq("guildId", guild.Key);
                    tasks.Add(GuildCollection.ReplaceOneAsync(filter, guild.Value, new ReplaceOptions()
                    {
                        IsUpsert = true
                    }));
                }

                Task.WaitAll(tasks.ToArray());
            }
        }

        public static async Task<BasicBot.Settings.Guild> GetDiscordOrMake(SocketGuild gld)
        {
            return await GetDiscordOrMake(gld.Id);
        }

        public static async Task<BasicBot.Settings.Guild> GetDiscordOrMake(IGuild gld)
        {
            return await GetDiscordOrMake(gld.Id);
        }

        public static async Task<BasicBot.Settings.Guild> GetDiscordOrMake(ulong guildID)
        {
            if (Guilds == null)
                Setup();

            var guild = await GetDiscord(guildID);
            if (guild == null)
            {
                return await MakeDiscord(guildID);
            }

            return guild;
        }

        public static async Task<BasicBot.Settings.Guild> GetDiscord(SocketGuild gld)
        {
            return await GetDiscord(gld.Id);
        }

        public static async Task<BasicBot.Settings.Guild> GetDiscord(IGuild gld)
        {
            return await GetDiscord(gld.Id);
        }

        public static async Task<BasicBot.Settings.Guild> GetDiscord(ulong guildID)
        {
            if (Guilds == null)
                Setup();
            if (Guilds.ContainsKey(guildID))
            {
                return Guilds[guildID];
            }

            var filter = Builders<BasicBot.Settings.Guild>.Filter.Eq("guildID", guildID);

            var guild = (await GuildCollection.FindAsync(x => x.guildId == guildID)).FirstOrDefault();

            if (guild != null)
                Guilds.Add(guildID, guild);

            return guild;
        }

        public static async Task<BasicBot.Settings.Guild> MakeDiscord(ulong guildId)
        {
            if (Guilds == null)
                Setup();

            if (!Guilds.ContainsKey(guildId))
            {
                var guild = new BasicBot.Settings.Guild()
                {
                    guildId = guildId
                };

                Guilds.Add(guildId, guild);

                await GuildCollection.InsertOneAsync(guild);
            }

            return Guilds[guildId];
        }

        #endregion user settings

        public static BasicBot.Settings.Guild.StaffRoles GetStaffRoles(ulong guildId)
        {
            return new BasicBot.Settings.Guild.StaffRoles();
            // var discord = GetDiscordOrMake(guildId);
            //
            // if (discord == null)
            // {
            // }
            //
            // return discord.StaffRole;
        }
    }
}
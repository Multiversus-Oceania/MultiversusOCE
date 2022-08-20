using Discord.WebSocket;
using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BasicBot.Settings;
using static BasicBot.Handler.String;

namespace BasicBot.Handler
{
    public static class Settings
    {
        private static Bot BotSettings;

        private static readonly string SettingsFile = CombineCurrentDirectory("settings.json");

        #region bot settings
        private static Bot LoadSettings()
        {
            if (File.Exists(SettingsFile))
            {
                var jsonText = File.ReadAllText(SettingsFile);
                if (!string.IsNullOrWhiteSpace(jsonText))
                {
                    return JsonConvert.DeserializeObject<Bot>(jsonText);
                }
            }
            else
            {
                BotSettings = new Bot { BotToken = "Put discord bot token here", BotOwners = new List<ulong> { 0, 1 }, BotPrefix = "?", StartGGToken = "Put start.gg token here"};
                SaveSettings();
            }
            return null;
        }

        public static Bot GetSettings()
        {
            if (BotSettings == null)
            {
                BotSettings = LoadSettings();
            }
            return BotSettings;
        }

        private static bool SaveSettings()
        {
            if (BotSettings != null)
            {
                var jsonText = JsonConvert.SerializeObject(BotSettings, Formatting.Indented);
                if (!string.IsNullOrWhiteSpace(jsonText))
                {
                    File.WriteAllText(SettingsFile, jsonText);
                    return true;
                }
            }
            return false;
        }

        public static string GetPrefix() =>
            BotSettings.BotPrefix;

        public static bool IsBotOwner(ulong id) =>
            BotSettings.BotOwners.Any(x => x == id);

        public static List<ulong> GetBotOwners() =>
            BotSettings.BotOwners;

        #endregion bot settings
    }
}

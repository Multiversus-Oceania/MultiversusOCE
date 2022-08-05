using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BasicBot.GraphQL;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using static BasicBot.MonarkTypes.Message;

namespace BasicBot.Handler
{
    public static class Multiversus
    {
        public static Dictionary<ulong, gamething> things = new();

        public static gamething GetThing(ulong thing)
        {
            if (things.ContainsKey(thing))
                return things[thing];
            return null;
        }

        public class gamething
        {
            public string BlockedMap = "";
            public ulong GuildId;
            public string SelectedMap = "";
            public SocketUser User1;
            public SocketUser User2;
            public List<string> MapPool;

            public gamething(SocketUser user1, SocketUser user2, IUserMessage message, ulong guild)
            {
                User1 = user1;
                User2 = user2;
                Message = message;
                GuildId = guild;
            }

            public IUserMessage Message { get; set; }


            public BasicBot.Settings.Guild gld => Guild.GetDiscordOrMake(GuildId);

            public Dictionary<string, List<string>> Maps => gld.Maps;

            public bool IsTurn(SocketUser user)
            {
                var turn = BlockedMap == "";

                return (turn && User1.Id == user.Id) || (!turn && User2.Id == user.Id);
            }

            public void AddMapBanned(SocketUser user, string mapBan)
            {
                if (user.Id == User1.Id)
                {
                    MapPool.Remove(mapBan);
                    BlockedMap = mapBan;
                }
            }

            public async Task<bool> SelectMap(SocketUser user, string map)
            {
                if (IsTurn(user))
                {
                    if (BlockedMap == "")
                    {
                        AddMapBanned(user, map);
                        await BuildSelectPhase().UpdateMessage(Message);
                    }
                    else
                    {
                        SelectedMap = map;
                        await BuildDonePhase().UpdateMessage(Message);
                    }

                    return true;
                }

                return false;
            }

            public MonarkMessage BuildSelectPhase()
            {
                var msg = new MonarkMessage();
                msg.Components = new ComponentBuilder()
                    .WithSelectMenus("bans", BuildBanSelectOptions(), "Pick a map to select")
                    .WithButton("Restart Map Selection", "restart", ButtonStyle.Danger).Build();
                msg.AddEmbed(new EmbedBuilder().WithTitle("Please select a map to play").AddField($"{User1.Username}",
                    $"Map Banned:\n{BlockedMap}", true).AddField($"{User2.Username} (Your Turn)",
                    "Map Selected", true));


                return msg;
            }

            public MonarkMessage BuildPoolPhase()
            {
                if (Maps.Count == 0) return "There are no map pools created";

                if (Maps.Count == 1) return BuildBanPhase(Maps.First().Value);

                var message = new MonarkMessage();
                message.AddEmbed(new EmbedBuilder().WithTitle("Please select a map pool"));
                message.Components = new ComponentBuilder().WithSelectMenus("maps", BuildSelectOptions())
                    .WithButton("Restart Map Selection", "restart", ButtonStyle.Danger).Build();

                return message;
            }

            public MonarkMessage BuildBanPhase(List<string> mapPool)
            {
                MapPool = mapPool;

                var msg = new MonarkMessage();
                if (MapPool != null && MapPool.Count < 2)
                {
                    msg.AddEmbed(new EmbedBuilder().WithTitle("Error")
                        .AddField("Error", "An error has occurred. The map pool is empty."));
                    return msg;
                }

                msg.Components = new ComponentBuilder()
                    .WithSelectMenus("bans", BuildBanSelectOptions(), "Pick a map to ban")
                    .WithButton("Restart Map Selection", "restart", ButtonStyle.Danger).Build();
                msg.AddEmbed(new EmbedBuilder().WithTitle("Please select a map to ban").AddField(
                    $"{User1.Username} (Your Turn)",
                    $"Map Banned:\n{BlockedMap}", true).AddField($"{User2.Username}",
                    "Map selected:", true));
                return msg;
            }

            public MonarkMessage BuildDonePhase()
            {
                var msg = new MonarkMessage();
                msg.Components = new ComponentBuilder()
                    .WithButton("New Game", "restart")
                    .WithButton("End set", "end", ButtonStyle.Danger).Build();
                msg.AddEmbed(new EmbedBuilder().WithDescription("Done").AddField($"{User1.Username}",
                    $"Maps Banned:\n{BlockedMap}", true).AddField($"{User2.Username}",
                    $"Maps Selected:\n{SelectedMap}", true));


                return msg;
            }

            public List<SelectMenuOptionBuilder> BuildBanSelectOptions()
            {
                var options = new List<SelectMenuOptionBuilder>();

                foreach (var a in MapPool)
                    options.Add(new SelectMenuOptionBuilder(a, a));

                return options;
            }

            public MonarkMessage BuildFirst()
            {
                try
                {
                    var message = new MonarkMessage();
                    message.AddEmbed(new EmbedBuilder().WithTitle("Please select the state of the game."));
                    message.Components =
                        new ComponentBuilder()
                            .WithButton("First game of the set", "coinflip")
                            .WithButton($"{User1.Username} won last game", "wonlast1", ButtonStyle.Success)
                            .WithButton($"{User2.Username} won last game", "wonlast2", ButtonStyle.Success)
                            .WithButton("End set", "end", ButtonStyle.Danger).Build();
                    return message;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }

            public List<SelectMenuOptionBuilder> BuildSelectOptions()
            {
                var options = new List<SelectMenuOptionBuilder>();

                foreach (var a in Maps.Keys) options.Add(new SelectMenuOptionBuilder(a, a));

                return options;
            }
        }

        public static Dictionary<StartID, Dictionary<StartID, Set>> sets = new();
        public static Dictionary<StartID, SocketCategoryChannel> runningEvents = new();

        public static async void UpdateSets()
        {
            foreach (var e in runningEvents)
            {
                var eventId = e.Key;
                var category = e.Value;

                var perPage = 20;

                var req = await StartGGHandler.Client.GetSetsAndLinkedAccounts.ExecuteAsync(eventId, 1, perPage);

                // Check still admin.
                if (category == null || req.Data.Event.Id == null ||
                    req.Data.Event.Tournament.Admins.Where(x => x.Id == req.Data.CurrentUser.Id).Count() != 1)
                {
                    Console.Error.WriteLine("Error occurred updating sets.");
                    // Remove existing sets from memory.
                    if (sets.ContainsKey(eventId))
                    {
                        sets[eventId].Clear();
                    }

                    break;
                }

                // Get Sets
                List<IGetSetsAndLinkedAccounts_Event_Sets_Nodes> eventSets = new();
                eventSets.AddRange(req.Data.Event.Sets.Nodes);
                // Get all the sets.
                if (eventSets.Count == perPage)
                {
                    var i = 2;
                    while (true)
                    {
                        var setsReq =
                            await StartGGHandler.Client.GetSetsAndLinkedAccounts.ExecuteAsync(eventId, i, perPage);
                        eventSets.AddRange(setsReq.Data.Event.Sets.Nodes);
                        if (setsReq.Data.Event.Sets.Nodes.Count != perPage || i == (int)MathF.Ceiling(1000f / perPage))
                            break;
                        i++;
                    }
                }

                Console.WriteLine(eventSets.Count);

                // Remove sets from the existing list if they aren't going still (Remove discord too)
                // Add new sets to the list (Start discord)
                // Check if games have changed on existing sets (New map selection in discord.)

                if (!sets.ContainsKey(req.Data.Event.Id ?? 0))
                {
                    var s = new Dictionary<StartID, Set>();
                    foreach (var set in eventSets)
                    {
                        if (Set.IsInProgress(set))
                        {
                            s.Add(set.Id ?? 0, await Set.CreateSet(category, set));
                        }
                    }

                    sets.Add(req.Data.Event.Id ?? 0, s);
                }
                else
                {
                    foreach (var set in eventSets)
                    {
                        // if ()
                    }
                }
            }
        }

        public static Set GetSet(StartID eventId, StartID setId)
        {
            if (sets.ContainsKey(eventId))
            {
                if (sets[eventId].ContainsKey(setId))
                {
                    return sets[eventId][setId];
                }
            }

            return null;
        }

        public class Set
        {
            public StartID StartId;
            public int CurrentGame;
            public gamething Gamething;
            public RestTextChannel Channel;
            public List<SocketUser> Team1 = new();
            public List<SocketUser> Team2 = new();

            public static bool IsInProgress(IGetSetsAndLinkedAccounts_Event_Sets_Nodes setInfo)
            {
                if (setInfo.Id == null)
                    return false;

                if (setInfo.Slots.Count != 2)
                    return false;


                foreach (var slot in setInfo.Slots)
                {
                    if (slot.Entrant == null)
                        return false;
                }

                return true;
            }


            public static async Task<Set> CreateSet(SocketCategoryChannel category,
                IGetSetsAndLinkedAccounts_Event_Sets_Nodes setInfo)
            {
                if (setInfo.Id == null)
                {
                    throw new NullReferenceException("Set id was null.");
                }

                var set = new Set();
                set.StartId = setInfo.Id ?? 0;
                set.CurrentGame = 0;

                // Get the discord users out of the set info.
                for (var i = 0; i < 2; i++)
                {
                    foreach (var participant in setInfo.Slots[i].Entrant.Participants)
                    {
                        foreach (var connection in participant.RequiredConnections)
                        {
                            if (connection.Type != AuthorizationType.Discord) continue;

                            if (ulong.TryParse(connection.Id,
                                    out var id))
                            {
                                SocketUser user = category.Guild.GetUser(id);
                                if (i == 0)
                                    set.Team1.Add(user);
                                else
                                    set.Team2.Add(user);
                            }
                        }
                    }
                }

                var channel = await category.Guild.CreateTextChannelAsync(
                    $"{setInfo.Slots[0].Entrant.Name} vs {setInfo.Slots[1].Entrant.Name}",
                    x =>
                    {
                        x.CategoryId = category.Id;

                        // Set permission overrides.
                        var allowedPermissions =
                            new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow);
                        var deniedPermissions =
                            new OverwritePermissions(viewChannel: PermValue.Deny, sendMessages: PermValue.Deny);

                        var overrides = new List<Overwrite>
                        {
                            new(category.Guild.EveryoneRole.Id, PermissionTarget.Role,
                                deniedPermissions)
                        };

                        foreach (var user in set.Team1)
                        {
                            overrides.Add(new Overwrite(user.Id, PermissionTarget.User, allowedPermissions));
                        }

                        foreach (var user in set.Team2)
                        {
                            overrides.Add(new Overwrite(user.Id, PermissionTarget.User, allowedPermissions));
                        }

                        x.PermissionOverwrites = overrides;
                    });

                set.Channel = channel;

                var _msg = new MonarkMessage();
                _msg.AddEmbed(new EmbedBuilder().WithTitle("Building..."));
                var msg = await _msg.SendMessage(channel);

                var gamething = new gamething(set.Team1[0], set.Team2[0], msg,
                    category.Guild.Id);

                things[msg.Id] = gamething;

                gamething.BuildFirst().UpdateMessage(msg);

                return set;
            }
        }
    }
}
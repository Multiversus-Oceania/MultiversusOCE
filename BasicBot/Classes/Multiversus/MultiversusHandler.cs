using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BasicBot.GraphQL.SetsAndLinkedAccounts;
using BasicBot.Handler;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json;
using static BasicBot.MonarkTypes.Message;
using Guild = BasicBot.Settings.Guild;

namespace BasicBot.Multiversus
{
    public static class Multiversus
    {
        public static Dictionary<ulong, Game> Games = new();

        public static Game GetGame(ulong thing)
        {
            if (Games.ContainsKey(thing))
                return Games[thing];
            return null;
        }

        public static Dictionary<string, Dictionary<string, Set>> Sets = new();
        public static Dictionary<string, SocketCategoryChannel> RunningEvents = new();
        public static List<(Set, DateTime)> ScheduledForDeletion = new();

        public static async Task Log(SocketGuild guild, string title, string text,
            Guild.ChannelType channel = Guild.ChannelType.Log, string plainText = "")
        {
            try
            {
                var gld = await Handler.Guild.GetDiscord(guild.Id);
                if (gld != null)
                {
                    if (gld.Channels.ContainsKey(channel))
                    {
                        var channelList =
                            guild.TextChannels.Where(x => x.Id == gld.Channels[channel]);
                        if (channelList.Count() == 1)
                        {
                            var msg = new MonarkMessage();
                            msg.Content = plainText;
                            msg.AddEmbed(new EmbedBuilder().WithTitle(title).WithDescription(text));
                            await msg.SendMessage(channelList.First(), AllowedMentionTypes.Roles);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(JsonConvert.SerializeObject(ex, Formatting.Indented));
            }
        }

        public static async Task LogException(SocketGuild guild, Exception ex)
        {
            await Log(guild, ex.Message,
                "```json\n" + JsonConvert.SerializeObject(ex, Formatting.Indented) + "\n```");
            Console.WriteLine(JsonConvert.SerializeObject(ex, Formatting.Indented));
        }

        public static async void UpdateSets()
        {
            var eventsToRemove = new List<string>();
            while (true)
            {
                try
                {
                    for (var i = 0; i < ScheduledForDeletion.Count; i++)
                    {
                        if ((ScheduledForDeletion[i].Item2 - DateTime.Now).TotalMinutes < 0)
                        {
                            try
                            {
                                // Ready for deletion.
                                var set = ScheduledForDeletion[i].Item1;
                                Games.Remove(set.Game.Message.Id);
                                await set.Channel.DeleteAsync();
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }

                            // Removes from this list and decreases i to compensate.
                            ScheduledForDeletion.RemoveAt(0);
                            i--;
                        }
                        else
                        {
                            break;
                        }
                    }

                    foreach (var e in RunningEvents)
                    {
                        try
                        {
                            var eventId = e.Key;
                            var category = e.Value;

                            var perPage = 20;

                            StartGGHandler.GetSetsAndLinkedAccounts req = null;

                            try
                            {
                                req = new StartGGHandler.GetSetsAndLinkedAccounts(eventId, 1, perPage);
                            }
                            catch (Exception exception)
                            {
                                Console.WriteLine(exception);
                                continue;
                            }

                            // Check still admin and category still exists.
                            if (category == null || req.Data.Event.Tournament.Admins == null ||
                                req.Data.Event.Tournament.Admins.Where(x => x.Id == req.Data.CurrentUser.Id)
                                    .Count() !=
                                1)
                            {
                                Console.Error.WriteLine("Error occurred updating sets.");
                                // Remove existing sets from memory.
                                if (Sets.ContainsKey(eventId))
                                {
                                    Sets[eventId].Clear();
                                }

                                break;
                            }

                            // Get Sets from the first request.
                            List<Node> startSets = new();
                            startSets.AddRange(req.Data.Event.Sets.Nodes);
                            // Get all the sets.
                            // Max 20 sets per request to be safe from overflowing the maximum objects per request.
                            // Keep requesting until receive a page with less than the amount per page requested which
                            // signifies the end of the sets.
                            // Max out at 1000 requests to be safe from issues and not loop forever.
                            if (startSets.Count == perPage)
                            {
                                // Start at 2 because the first request already contains the info for page 1.
                                var i = 2;
                                while (true)
                                {
                                    // This should really be its own query that doesnt get the self, event and tournament info but oh well.

                                    StartGGHandler.GetSetsAndLinkedAccounts setsRequest = null;

                                    try
                                    {
                                        setsRequest = new StartGGHandler.GetSetsAndLinkedAccounts(eventId, i, perPage);
                                    }
                                    catch (Exception exception)
                                    {
                                        Console.WriteLine(exception);
                                        continue;
                                    }

                                    foreach (var startSet in setsRequest.Data.Event.Sets.Nodes)
                                    {
                                        // Ensure that there are 2 filled slots in the set.
                                        // Start will send sets that are "in progress" with one player in them.
                                        if (Set.IsInProgress(startSet))
                                        {
                                            startSets.Add(startSet);
                                        }
                                    }

                                    if (setsRequest.Data.Event.Sets.Nodes.Count != perPage ||
                                        i == (int)MathF.Ceiling(1000f / perPage))
                                        break;
                                    i++;
                                }
                            }

                            // No sets means that the tournament is over.
                            if (startSets.Count == 0)
                            {
                                eventsToRemove.Add(eventId);
                                continue;
                            }

                            // Remove sets from the existing list if they aren't going still (Remove discord too)
                            // Add new sets to the list (Start discord)
                            // Check if games have changed on existing sets (New map selection in discord.)

                            // If the dictionary does already have sets for this event, no point checking as all sets will be new.
                            if (!Sets.ContainsKey(eventId))
                            {
                                var newSets = new Dictionary<string, Set>();
                                foreach (var startSet in startSets)
                                {
                                    try
                                    {
                                        var set = await Set.CreateSet(category, startSet);
                                        if (set != null)
                                        {
                                            newSets.Add(startSet.Id, set);
                                            set.Update(category.Guild, startSet);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine(ex);
                                    }
                                }

                                Sets.Add(eventId, newSets);
                            }
                            else
                            {
                                var foundSets = new Dictionary<string, Set>();
                                var existingSets = Sets[eventId];
                                foreach (var startSet in startSets)
                                {
                                    if (startSet.Id == null) continue;
                                    var setId = startSet.Id;

                                    // Existing game. Check for a change in games played.
                                    if (existingSets.ContainsKey(setId))
                                    {
                                        try
                                        {
                                            var set = existingSets[setId];
                                            foundSets.Add(setId, set);
                                            existingSets.Remove(setId);

                                            set.Update(category.Guild, startSet);
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine(ex);
                                        }
                                    }
                                    // New set started. Create set object and add to dictionary.
                                    else
                                    {
                                        try
                                        {
                                            var set = await Set.CreateSet(category, startSet);
                                            if (set != null)
                                                foundSets.Add(setId, set);
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine(ex);
                                        }
                                    }
                                }

                                // Each set left in existing sets has ended.
                                foreach (var set in existingSets)
                                {
                                    // Handle set end.
                                    // Delay for ggs (5 mins).
                                    // Delete discord channel.
                                    ScheduledForDeletion.Add((set.Value, DateTime.Now.AddMinutes(5)));

                                    var _msg = new MonarkMessage();
                                    _msg.AddEmbed(new EmbedBuilder().WithTitle("Thank you for playing.")
                                        .WithFooter("This channel will be automatically deleted in 5 minutes."));

                                    _msg.SendMessage(set.Value.Channel);
                                }

                                // Assign new list of sets to the sets dict.
                                Sets[eventId] = foundSets;
                            }
                        }
                        catch (Exception ex)
                        {
                            await LogException(e.Value.Guild, ex);
                        }
                    }

                    for (var i = 0; i < eventsToRemove.Count; i++)
                    {
                        RunningEvents.Remove(eventsToRemove[i]);
                    }

                    eventsToRemove.Clear();

                    await InProgressBoard.UpdateBoards();

                    await Task.Delay(5 * 1000);
                    Console.WriteLine("Delay done.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(JsonConvert.SerializeObject(ex, Formatting.Indented));
                }
            }
        }

        public static Set GetSet(string eventId, string setId)
        {
            if (Sets.ContainsKey(eventId))
            {
                if (Sets[eventId].ContainsKey(setId))
                {
                    return Sets[eventId][setId];
                }
            }

            return null;
        }

        public class Set
        {
            public int CurrentGame;
            public int WonLast;
            public List<string> SelectedMaps = new();
            public Game Game;
            public RestTextChannel Channel;
            public Team Team1;
            public Team Team2;
            public int[] Wins = new int[2];
            public DateTime LastEvent;
            public bool CheckedIn;

            public static bool IsInProgress(Node setInfo)
            {
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
                Node setInfo)
            {
                try
                {
                    if (setInfo.Slots[0].Entrant == null || setInfo.Slots[1].Entrant == null) return null;
                    
                    var set = new Set();
                    set.CurrentGame = 1;
                    set.WonLast = 0;
                    set.Team1 = new Team(setInfo.Slots[0].Entrant, category.Guild);
                    set.Team2 = new Team(setInfo.Slots[1].Entrant, category.Guild);
                    set.LastEvent = DateTime.Now;
                    set.CheckedIn = false;

                    if (set.Team1.Id == "" || set.Team2.Id == "")
                    {
                        return null;
                    }

                    var gld = await Handler.Guild.GetDiscordOrMake(category.Guild);

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

                            foreach (var user in set.Team1.Users)
                            {
                                if (user != null)
                                    overrides.Add(new Overwrite(user.Id, PermissionTarget.User, allowedPermissions));
                            }

                            foreach (var user in set.Team2.Users)
                            {
                                if (user != null)
                                    overrides.Add(new Overwrite(user.Id, PermissionTarget.User, allowedPermissions));
                            }

                            foreach (var role in gld.GameRoomRoles)
                            {
                                overrides.Add(new Overwrite(role, PermissionTarget.Role, allowedPermissions));
                            }

                            x.PermissionOverwrites = overrides;
                        });

                    set.Channel = channel;

                    set.SendGameMessage(true);

                    return set;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(JsonConvert.SerializeObject(ex, Formatting.Indented));
                }

                return null;
            }

            public async Task Update(SocketGuild guild, Node startSet)
            {
                try
                {
                    if (LastEvent + TimeSpan.FromMinutes(15) < DateTime.Now)
                    {
                        var ping = "";
                        var gld = await Handler.Guild.GetDiscordOrMake(guild);

                        foreach (var role in gld.ModPingRoles)
                        {
                            ping = MentionUtils.MentionRole(role) + " " + ping;
                        }

                        await Log(guild, Channel.Name, $"<#{Channel.Id}> has not been updated in 15 minutes.",
                            Guild.ChannelType.Ping, ping);

                        LastEvent = DateTime.Now;
                    }

                    if (!CheckedIn)
                    {
                        if (startSet.State == 2)
                        {
                            CheckedIn = true;
                            await UpdateMessage(Game.Message);
                        }
                    }

                    // Check if games have changed.
                    // If so, create new message.
                    // Before modifying old game, get the selected map and save it.

                    if (startSet.Games != null)
                    {
                        // Handle both potential behaviors of either adding to array after each game or by having
                        // the array always be filled with null games and setting them after games are played.
                        if (startSet.Games.Count >= CurrentGame && startSet.Games.Count < startSet.TotalGames)
                        {
                            var team1Wins = 0;
                            var team2Wins = 0;
                            LastEvent = DateTime.Now;
                            for (var i = 0; i < startSet.Games.Count; i++)
                            {
                                if (startSet.Games[i] != null)
                                {
                                    // Convenient to also set wonLast here too even though it will be overridden by the next
                                    // loop.
                                    var wonLast = 0;
                                    if (startSet.Games[i].WinnerId == Team1.Id)
                                    {
                                        wonLast = 1;
                                        team1Wins++;
                                    }
                                    else if (startSet.Games[i].WinnerId == Team2.Id)
                                    {
                                        wonLast = 2;
                                        team2Wins++;
                                    }

                                    // i is the number of games played - 1.
                                    if (i + 1 >= CurrentGame && i + 1 < startSet.TotalGames)
                                    {
                                        if (team1Wins < startSet.TotalGames / 2f &&
                                            team2Wins < startSet.TotalGames / 2f)
                                            await NextGame(wonLast);
                                    }
                                }
                            }

                            Wins = new[] { team1Wins, team2Wins };
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(JsonConvert.SerializeObject(ex, Formatting.Indented));
                }
            }

            public async Task NextGame(int wonLast)
            {
                WonLast = wonLast;
                CurrentGame++;
                SelectedMaps.Add(Game.SelectedMap);

                var _msg = Game.BuildDonePhase();
                _msg.Components = null;
                await _msg.UpdateMessage(Game.Message);

                Games.Remove(Game.Message.Id);

                await SendGameMessage();
            }

            public async Task SendGameMessage(bool ping = false)
            {
                if (ping)
                {
                    var pings = "";
                    foreach (var user in Team1.Users)
                    {
                        pings += user.Mention + " ";
                    }

                    foreach (var user in Team2.Users)
                    {
                        pings += user.Mention + " ";
                    }

                    await Channel.SendMessageAsync(pings);
                }

                var _msg = new MonarkMessage();
                _msg.AddEmbed(new EmbedBuilder().WithTitle(CheckedIn
                    ? "Building..."
                    : "Please Check In on start.gg and return here."));
                var msg = await _msg.SendMessage(Channel);

                Game = new Game(this, msg);

                Games[msg.Id] = Game;

                if (CheckedIn)
                {
                    await UpdateMessage(msg);
                }
            }

            private async Task UpdateMessage(IUserMessage msg)
            {
                // If the last game was manually set, choose who won last game.
                if (WonLast != 0 || CurrentGame == 1)
                    await (await Game.BuildPoolPhase()).UpdateMessage(msg);
                else
                    await Game.BuildFirst().UpdateMessage(msg);
            }
        }
    }
}
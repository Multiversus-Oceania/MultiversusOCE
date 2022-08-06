using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BasicBot.GraphQL.SetsAndLinkedAccounts;
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
            public SocketUser[] Team1;
            public string Team1Name;
            public SocketUser[] Team2;
            public string Team2Name;
            public List<string> MapPool;
            public Set Set;

            public gamething(SocketUser team1, SocketUser team2,
                IUserMessage message, ulong guild)
            {
                Team1 = new[] { team1 };
                Team1Name = team1.Username;
                Team2 = new[] { team2 };
                Team2Name = team2.Username;
                Message = message;
                GuildId = guild;
                Set = null;
            }

            public gamething(Set set, IUserMessage message)
            {
                var team1Bans = set.CurrentGame != 1 ? set.WonLast == 1 : Random.RandomBool();
                if (team1Bans)
                {
                    Team1 = set.Team1.Users.ToArray();
                    Team1Name = set.Team1.Name;
                    Team2 = set.Team2.Users.ToArray();
                    Team2Name = set.Team2.Name;
                }
                else
                {
                    Team1 = set.Team2.Users.ToArray();
                    Team1Name = set.Team2.Name;
                    Team2 = set.Team1.Users.ToArray();
                    Team2Name = set.Team1.Name;
                }

                Message = message;
                GuildId = set.Channel.GuildId;
                Set = set;
            }

            public IUserMessage Message { get; set; }


            public BasicBot.Settings.Guild gld => Guild.GetDiscordOrMake(GuildId);

            public Dictionary<string, List<string>> Maps => gld.Maps;

            public bool OnTeam(SocketUser user, SocketUser[] team)
            {
                foreach (var teamMember in team)
                {
                    if (teamMember.Id == user.Id) return true;
                }

                return false;
            }

            public bool IsTurn(SocketUser user)
            {
                var turn = BlockedMap == "";

                return (turn && OnTeam(user, Team1)) || (!turn && OnTeam(user, Team2));
            }

            public void AddMapBanned(SocketUser user, string mapBan)
            {
                if (OnTeam(user, Team1))
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
                msg.AddEmbed(new EmbedBuilder().WithTitle("Please select a map to play").AddField($"{Team1Name}",
                    $"Map Banned:\n{BlockedMap}", true).AddField($"{Team2Name} (Your Turn)",
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
                    $"{Team1Name} (Your Turn)",
                    $"Map Banned:\n{BlockedMap}", true).AddField($"{Team2Name}",
                    "Map selected:", true));
                return msg;
            }

            public MonarkMessage BuildDonePhase()
            {
                var msg = new MonarkMessage();
                var components = new ComponentBuilder();
                if (Set == null)
                {
                    components.WithButton("New Game", "restart").WithButton("End set", "end", ButtonStyle.Danger);
                }
                else
                {
                    components.WithButton("Undo Selection", "restart", ButtonStyle.Danger)
                        .WithButton(
                            "Manually Start Next Game (Only use if it does not start after reporting scores on start.gg)",
                            "next", ButtonStyle.Danger);
                }

                msg.Components = components.Build();

                msg.AddEmbed(new EmbedBuilder().WithDescription("Done").AddField($"{Team1Name}",
                    $"Maps Banned:\n{BlockedMap}", true).AddField($"{Team2Name}",
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
                            .WithButton($"{Team1Name} won last game", "wonlast1", ButtonStyle.Success)
                            .WithButton($"{Team2Name} won last game", "wonlast2", ButtonStyle.Success)
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

        public static Dictionary<string, Dictionary<string, Set>> sets = new();
        public static Dictionary<string, SocketCategoryChannel> runningEvents = new();
        public static List<(Set, DateTime)> scheduledForDeletion = new();

        public static async void UpdateSets()
        {
            var eventsToRemove = new List<string>();
            while (true)
            {
                for (var i = 0; i < scheduledForDeletion.Count; i++)
                {
                    if ((scheduledForDeletion[i].Item2 - DateTime.Now).TotalMinutes < 0)
                    {
                        // Ready for deletion.
                        var set = scheduledForDeletion[i].Item1;
                        things.Remove(set.Gamething.Message.Id);
                        await set.Channel.DeleteAsync();
                    }
                    else
                    {
                        // Since times are ordered, anything after the first failure doesn't need to be checked.
                        // Remove all the deleted items before this one.
                        scheduledForDeletion.RemoveRange(0, i);
                        break;
                    }
                }

                foreach (var e in runningEvents)
                {
                    var eventId = e.Key;
                    var category = e.Value;

                    var perPage = 20;

                    var req = new StartGGHandler.GetSetsAndLinkedAccounts(eventId, 1, perPage);

                    // Check still admin and category still exists.
                    if (false && (category == null || req.Data.Event.Tournament.Admins == null ||
                                  req.Data.Event.Tournament.Admins.Where(x => x.Id == req.Data.CurrentUser.Id)
                                      .Count() !=
                                  1))
                    {
                        Console.Error.WriteLine("Error occurred updating sets.");
                        // Remove existing sets from memory.
                        if (sets.ContainsKey(eventId))
                        {
                            sets[eventId].Clear();
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
                            var setsRequest = new
                                StartGGHandler.GetSetsAndLinkedAccounts(eventId, i, perPage);

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
                    if (!sets.ContainsKey(eventId))
                    {
                        var newSets = new Dictionary<string, Set>();
                        foreach (var startSet in startSets)
                        {
                            newSets.Add(startSet.Id, await Set.CreateSet(category, startSet));
                        }

                        sets.Add(eventId, newSets);
                    }
                    else
                    {
                        var foundSets = new Dictionary<string, Set>();
                        var existingSets = sets[eventId];
                        foreach (var startSet in startSets)
                        {
                            if (startSet.Id == null) continue;
                            var setId = startSet.Id;

                            // Existing game. Check for a change in games played.
                            if (existingSets.ContainsKey(setId))
                            {
                                var set = existingSets[setId];
                                foundSets.Add(setId, set);
                                existingSets.Remove(setId);

                                set.Update(startSet);
                            }
                            // New set started. Create set object and add to dictionary.
                            else
                            {
                                foundSets.Add(setId, await Set.CreateSet(category, startSet));
                            }
                        }

                        // Each set left in existing sets has ended.
                        foreach (var set in existingSets)
                        {
                            // Handle set end.
                            // Delay for ggs (5 mins).
                            // Delete discord channel.
                            scheduledForDeletion.Add((set.Value, DateTime.Now.AddMinutes(5)));

                            var _msg = new MonarkMessage();
                            _msg.AddEmbed(new EmbedBuilder().WithTitle("Thank you for playing.")
                                .WithFooter("This channel will be automatically deleted in 5 minutes."));

                            _msg.SendMessage(set.Value.Channel);
                        }

                        // Assign new list of sets to the sets dict.
                        sets[eventId] = foundSets;
                    }
                }

                for (var i = 0; i < eventsToRemove.Count; i++)
                {
                    runningEvents.Remove(eventsToRemove[i]);
                }

                eventsToRemove.Clear();
            }
        }

        public static Set GetSet(string eventId, string setId)
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
            public struct Team
            {
                public string StartId;
                public List<SocketUser> Users;
                public string Name;

                public Team(Entrant entrant, SocketGuild guild)
                {
                    StartId = entrant.Id;
                    Users = new List<SocketUser>();
                    Name = entrant.Name;
                    // Get the discord users out of the set info.
                    foreach (var participant in entrant.Participants)
                    {
                        foreach (var connection in participant.RequiredConnections)
                        {
                            if (connection.Type != TypeEnum.Discord) continue;

                            if (ulong.TryParse(connection.ExternalId, out var id))
                            {
                                SocketUser user = guild.GetUser(id);
                                Users.Add(user);
                            }
                        }
                    }
                }
            }

            public int CurrentGame;
            public int WonLast;
            public List<string> SelectedMaps = new();
            public gamething Gamething;
            public RestTextChannel Channel;
            public Team Team1;
            public Team Team2;

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
                var set = new Set();
                set.CurrentGame = 1;
                set.WonLast = 0;
                set.Team1 = new Team(setInfo.Slots[0].Entrant, category.Guild);
                set.Team2 = new Team(setInfo.Slots[1].Entrant, category.Guild);

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
                            overrides.Add(new Overwrite(user.Id, PermissionTarget.User, allowedPermissions));
                        }

                        foreach (var user in set.Team2.Users)
                        {
                            overrides.Add(new Overwrite(user.Id, PermissionTarget.User, allowedPermissions));
                        }

                        x.PermissionOverwrites = overrides;
                    });

                set.Channel = channel;

                set.SendGameMessage(true);

                return set;
            }

            public async Task Update(Node startSet)
            {
                // Check if games have changed.
                // If so, create new message.
                // Before modifying old gamething, get the selected map and save it.

                if (startSet.Games != null)
                {
                    // Handle both potential behaviors of either adding to array after each game or by having
                    // the array always be filled with null games and setting them after games are played.
                    if (startSet.Games.Count >= CurrentGame)
                    {
                        for (var i = 0; i < startSet.Games.Count; i++)
                        {
                            if (startSet.Games[i] == null)
                            {
                                // i is the number of games played.
                                if (i >= CurrentGame && i + 1 < startSet.TotalGames)
                                {
                                    var wonLast = 0;
                                    if (startSet.Games[i].WinnerId == Team1.StartId)
                                        wonLast = 1;
                                    else if (startSet.Games[i].WinnerId == Team2.StartId)
                                        wonLast = 2;
                                    await NextGame(wonLast);
                                }
                            }
                        }
                    }
                }
            }

            public async Task NextGame(int wonLast)
            {
                WonLast = wonLast;
                CurrentGame++;
                SelectedMaps.Add(Gamething.SelectedMap);

                var _msg = Gamething.BuildDonePhase();
                _msg.Components = null;
                await _msg.UpdateMessage(Gamething.Message);

                things.Remove(Gamething.Message.Id);

                await SendGameMessage();
            }

            public async Task SendGameMessage(bool ping = false)
            {
                MonarkMessage _msg;
                if (ping)
                {
                    _msg = new MonarkMessage();
                    var pings = "";
                    foreach (var user in Team1.Users)
                    {
                        pings += user.Mention + " ";
                    }

                    foreach (var user in Team2.Users)
                    {
                        pings += user.Mention + " ";
                    }

                    await _msg.SendMessage(Channel);
                }

                _msg = new MonarkMessage();
                _msg.AddEmbed(new EmbedBuilder().WithTitle("Building..."));
                var msg = await _msg.SendMessage(Channel);

                Gamething = new gamething(this, msg);

                things[msg.Id] = Gamething;

                // If the last game was manually set, choose who won last game.
                if (WonLast != 0 || CurrentGame == 1)
                    await Gamething.BuildPoolPhase().UpdateMessage(msg);
                else
                    await Gamething.BuildFirst().UpdateMessage(msg);
            }
        }
    }
}
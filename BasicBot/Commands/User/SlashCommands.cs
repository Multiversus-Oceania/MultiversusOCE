#region

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BasicBot.Handler;
using BasicBot.MonarkTypes;
using BasicBot.Multiversus;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using static BasicBot.Multiversus.Multiversus;
using Game = BasicBot.Multiversus.Game;
using Guild = BasicBot.Settings.Guild;

#endregion

namespace BasicBot.Commands;

//[DontAutoRegister()]
public class SlashCommand : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
{
    [SlashCommand("add-map-pool", "Map pools to be added")]
    public async Task AddMapPool(string Name, List<string> Maps) //, )
    {
        await DeferAsync(true);
        var gld = await Handler.Guild.GetDiscordOrMake(Context.Guild);
        gld.Maps[Name] = Maps;

        await Context.Interaction.FollowupAsync("Done", ephemeral: true);
        Handler.Guild.SaveGuild(gld);
    }

    [SlashCommand("remove-map-pool", "Map pools to be removed")]
    public async Task RemoveMapPool(string Name) //, )
    {
        await DeferAsync(true);
        var gld = await Handler.Guild.GetDiscordOrMake(Context.Guild);
        if (!gld.Maps.ContainsKey(Name))
        {
            await Context.Interaction.FollowupAsync($"Failed to fine {Name}", ephemeral: true);
            return;
        }

        gld.Maps.Remove(Name);

        await Context.Interaction.FollowupAsync("removed", ephemeral: true);
        Handler.Guild.SaveGuild(gld);
    }

    [SlashCommand("set-tournament-category", "Set the category for auto created channels.")]
    public async Task setCategory(SocketCategoryChannel category)
    {
        await DeferAsync(true);
        var gld = await Handler.Guild.GetDiscordOrMake(Context.Guild);

        if (Context.Guild.CategoryChannels.Where(c => c.Id == category.Id).ToArray().Count() == 1)
        {
            gld.TournamentCategory = category.Id;
            Handler.Guild.SaveGuild(gld);
        }
        else
        {
            await Context.Interaction.FollowupAsync("Failed to set tournament category.", ephemeral: true);
            return;
        }


        await Context.Interaction.FollowupAsync("Set tournament category.", ephemeral: true);
    }

    [SlashCommand("game", "Create a game with the new system.")]
    public async Task game(SocketGuildUser enemy1, SocketGuildUser teammate = null, SocketGuildUser enemy2 = null)
    {
        await DeferAsync(true);
        var gld = await Handler.Guild.GetDiscordOrMake(Context.Guild);

        var team1Users = new List<SocketGuildUser>() { Context.User as SocketGuildUser };
        var team2Users = new List<SocketGuildUser>() { enemy1 };

        if (teammate != null) team1Users.Add(teammate);
        if (enemy2 != null) team2Users.Add(enemy2);

        // Cut out the first value of team1.
        foreach (var user in team1Users.GetRange(1, team1Users.Count - 1).Union(team2Users))
        {
            if (user.Id == Context.User.Id)
            {
                // await Context.Interaction.FollowupAsync("Cannot challenge yourself.", ephemeral: true);
                // return;
            }

            if (user.IsBot)
            {
                await Context.Interaction.FollowupAsync("Cannot challenge a bot.", ephemeral: true);
                return;
            }
        }

        if (Context.Guild.CategoryChannels.Where(c => c.Id == gld.TournamentCategory).ToArray().Length != 1)
        {
            await Context.Interaction.FollowupAsync(
                "The server administrator has set an invalid tournament category. Please contact an admin.",
                ephemeral: true);
            return;
        }

        var allowedPermissions =
            new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow);
        var deniedPermissions = new OverwritePermissions(viewChannel: PermValue.Deny, sendMessages: PermValue.Deny);

        var overwrites = new List<Overwrite>()
        {
            new(Context.Guild.EveryoneRole.Id, PermissionTarget.Role, deniedPermissions)
        };
        foreach (var user in team1Users.Union(team2Users))
            overwrites.Add(new Overwrite(user.Id, PermissionTarget.User, allowedPermissions));

        var team1 = new Team(team1Users);
        var team2 = new Team(team2Users);

        var channel = await Context.Guild.CreateTextChannelAsync($"{team1.Name} vs {team2.Name}".Replace("/", "-"),
            x =>
            {
                x.CategoryId = gld.TournamentCategory;

                x.PermissionOverwrites = overwrites.ToArray();
            });

        await Context.Interaction.FollowupAsync("Game channel created: " + channel.Mention, ephemeral: true);

        var _msg = new Message.MonarkMessage();
        _msg.AddEmbed(new EmbedBuilder().WithTitle("Building..."));
        var msg = await _msg.SendMessage(channel);

        var gamething = new Game(team1, team2, msg, Context.Guild.Id);
        Games[msg.Id] = gamething;
        await gamething.BuildFirst().UpdateMessage(msg);
    }

    [SlashCommand("remove-channels", "Create a game with the new system.")]
    public async Task removeChannels()
    {
        await DeferAsync(true);
        Games.Clear();

        var categoryId = (await Handler.Guild.GetDiscordOrMake(Context.Guild)).TournamentCategory;

        foreach (var category in Context.Guild.CategoryChannels)
        {
            if (category.Id == categoryId)
            {
                await Context.Interaction.FollowupAsync("Deleting...", ephemeral: true);
                foreach (var channel in category.Channels)
                {
                    await channel.DeleteAsync();
                }

                await Context.Interaction.ModifyOriginalResponseAsync(x => { x.Content = "Deleted channels"; });
                return;
            }
        }

        await Context.Interaction.FollowupAsync("Category could not be found.", ephemeral: true);
    }

    [SlashCommand("start-event", "Start an event hosted on start.gg")]
    public async Task startEvent(string url)
    {
        await DeferAsync(true);
        var gld = await Handler.Guild.GetDiscordOrMake(Context.Guild);

        var categoryId = gld.TournamentCategory;

        foreach (var category in Context.Guild.CategoryChannels)
        {
            if (category.Id == categoryId)
            {
                var urlParts = url.Split("/");

                for (var i = 0; i < urlParts.Length; i++)
                {
                    if (urlParts[i] == "tournament" && urlParts.Length >= i + 4)
                    {
                        if (urlParts[i + 2] == "event")
                        {
                            await DeferAsync(true);
                            var slug = urlParts[i] + "/" + urlParts[i + 1] + "/" + urlParts[i + 2] + "/" +
                                       urlParts[i + 3];

                            var eventId = new StartGGHandler.GetEventId(slug);
                            if (eventId.Data != null && eventId.Data.Event != null)
                            {
                                if (RunningEvents.ContainsKey(eventId.Data.Event.Id))
                                {
                                    await Context.Interaction.ModifyOriginalResponseAsync(
                                        x => x.Content = "Event already started.");
                                    RunningEvents.Add(eventId.Data.Event.Id, category);
                                }
                                else
                                {
                                    await Context.Interaction.ModifyOriginalResponseAsync(
                                        x => x.Content = "Starting Event: " + eventId.Data.Event.Name);
                                    RunningEvents.Add(eventId.Data.Event.Id, category);
                                }

                                return;
                            }
                        }
                    }
                }

                await Context.Interaction.FollowupAsync("Event not found.", ephemeral: true);
                return;
            }
        }

        await Context.Interaction.FollowupAsync("Please set the category for channels to be created in.",
            ephemeral: true);
    }

    [SlashCommand("stop-event", "Stop a running event.")]
    public async Task stopEvent(string url)
    {
        await DeferAsync(true);
        var gld = await Handler.Guild.GetDiscordOrMake(Context.Guild);

        var urlParts = url.Split("/");

        for (var i = 0; i < urlParts.Length; i++)
        {
            if (urlParts[i] == "tournament" && urlParts.Length >= i + 4)
            {
                if (urlParts[i + 2] == "event")
                {
                    await DeferAsync(true);
                    var slug = urlParts[i] + "/" + urlParts[i + 1] + "/" + urlParts[i + 2] + "/" +
                               urlParts[i + 3];

                    var eventId = new StartGGHandler.GetEventId(slug);
                    if (eventId.Data != null && eventId.Data.Event != null)
                    {
                        if (RunningEvents.ContainsKey(eventId.Data.Event.Id))
                        {
                            await Context.Interaction.ModifyOriginalResponseAsync(
                                x => x.Content = "Stopped Event: " + eventId.Data.Event.Name);
                            RunningEvents.Remove(eventId.Data.Event.Id);
                            Sets.Remove(eventId.Data.Event.Id);
                            return;
                        }

                        await Context.Interaction.ModifyOriginalResponseAsync(
                            x => x.Content = "Event not running.");
                    }
                }
            }
        }

        await Context.Interaction.FollowupAsync("Event not found.", ephemeral: true);
    }

    [SlashCommand("set-channel", "Set a channel.")]
    public async Task setChannel(Guild.ChannelType channelType, SocketTextChannel channel)
    {
        await DeferAsync(true);
        var gld = await Handler.Guild.GetDiscordOrMake(Context.Guild);

        if (Context.Guild.TextChannels.Where(c => c.Id == channel.Id).ToArray().Count() == 1)
        {
            if (gld.Channels.ContainsKey(channelType))
                gld.Channels[channelType] = channel.Id;
            else
                gld.Channels.Add(channelType, channel.Id);

            Handler.Guild.SaveGuild(gld);
        }
        else
        {
            await Context.Interaction.FollowupAsync($"Failed to set {channelType} channel.", ephemeral: true);
            return;
        }


        await Context.Interaction.FollowupAsync($"Set {channelType} channel.", ephemeral: true);
    }
}
#region

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BasicBot.Handler;
using BasicBot.MonarkTypes;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using static BasicBot.Handler.Multiversus;

#endregion

namespace BasicBot.Commands;

//[DontAutoRegister()]
public class SlashCommand : InteractionModuleBase<SocketInteractionContext<SocketSlashCommand>>
{
    [SlashCommand("add-map-pool", "Map pools to be added")]
    public async Task AddMapPool(string Name, List<string> Maps) //, )
    {
        var gld = Guild.GetDiscordOrMake(Context.Guild);
        gld.Maps[Name] = Maps;

        await Context.Interaction.RespondAsync("Done", ephemeral: true);
        Guild.SaveGuilds();
    }

    [SlashCommand("remove-map-pool", "Map pools to be removed")]
    public async Task RemoveMapPool(string Name) //, )
    {
        var gld = Guild.GetDiscordOrMake(Context.Guild);
        if (!gld.Maps.ContainsKey(Name))
        {
            await Context.Interaction.RespondAsync($"Failed to fine {Name}", ephemeral: true);
            return;
        }

        gld.Maps.Remove(Name);

        await Context.Interaction.RespondAsync("removed", ephemeral: true);
        Guild.SaveGuilds();
    }

    [SlashCommand("set-tournament-category", "Set the category for auto created channels.")]
    public async Task setCategory(ulong categoryId)
    {
        var gld = Guild.GetDiscordOrMake(Context.Guild);

        if (Context.Guild.CategoryChannels.Where(c => c.Id == categoryId).ToArray().Count() == 1)
        {
            gld.TournamentCategory = categoryId;
            Guild.SaveGuilds();
        }
        else
        {
            await Context.Interaction.RespondAsync("Failed to set tournament category.", ephemeral: true);
            return;
        }


        await Context.Interaction.RespondAsync("Set tournament category.", ephemeral: true);
    }

    [SlashCommand("game", "Create a game with the new system.")]
    public async Task game(SocketUser enemy)
    {
        var gld = Guild.GetDiscordOrMake(Context.Guild);

        // if (enemy.Id == Context.User.Id)
        // {
        //     await Context.Interaction.RespondAsync("Cannot challenge yourself.", ephemeral: true);
        //     return;
        // }

        if (enemy.IsBot)
        {
            await Context.Interaction.RespondAsync("Cannot challenge a bot.", ephemeral: true);
            return;
        }

        if (Context.Guild.CategoryChannels.Where(c => c.Id == gld.TournamentCategory).ToArray().Length != 1)
        {
            await Context.Interaction.RespondAsync(
                "The server administrator has set an invalid tournament category. Please contact an admin.",
                ephemeral: true);
            return;
        }

        var allowedPermissions =
            new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow);
        var deniedPermissions = new OverwritePermissions(viewChannel: PermValue.Deny, sendMessages: PermValue.Deny);

        var channel = await Context.Guild.CreateTextChannelAsync($"{Context.User.Username} vs {enemy.Username}",
            x =>
            {
                x.CategoryId = gld.TournamentCategory;

                x.PermissionOverwrites = new[]
                {
                    new(Context.User.Id, PermissionTarget.User, allowedPermissions),
                    new Overwrite(enemy.Id, PermissionTarget.User, allowedPermissions),
                    new Overwrite(Context.Guild.EveryoneRole.Id, PermissionTarget.Role, deniedPermissions)
                };
            });

        await Context.Interaction.RespondAsync("Game channel created: " + channel.Mention, ephemeral: true);

        var _msg = new Message.MonarkMessage();
        _msg.AddEmbed(new EmbedBuilder().WithTitle("Building..."));
        var msg = await _msg.SendMessage(channel);

        var gamething = new gamething(Context.Interaction.User, enemy, msg,
            Context.Guild.Id);

        things[msg.Id] = gamething;

        await gamething.BuildFirst().UpdateMessage(msg);
    }

    [SlashCommand("remove-channels", "Create a game with the new system.")]
    public async Task removeChannels()
    {
        await Context.Interaction.RespondAsync("Deleting...", ephemeral: true);

        things.Clear();

        foreach (var category in Context.Guild.CategoryChannels)
        {
            if (category.Id == 1003163620680683530)
            {
                foreach (var channel in category.Channels)
                {
                    channel.DeleteAsync();
                }

                break;
            }
        }
    }

    [SlashCommand("start-event", "Start an event hosted on start.gg")]
    public async Task startEvent(string slug)
    {
    }
}
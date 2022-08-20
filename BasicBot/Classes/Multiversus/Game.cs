using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BasicBot.Handler;
using BasicBot.MonarkTypes;
using Discord;
using Discord.WebSocket;
using Guild = BasicBot.Settings.Guild;
using Random = BasicBot.Handler.Random;

namespace BasicBot.Multiversus;

public class Game
{
    public string BlockedMap = "";
    public ulong GuildId;
    public string SelectedMap = "";
    public Team CoinflipWinner = null;
    public Team Team1;
    public Team Team2;
    public List<string> MapPool;
    public Multiversus.Set Set;

    public Game(Team team1, Team team2,
        IUserMessage message, ulong guild)
    {
        Team1 = team1;

        Team2 = team2;

        Message = message;
        GuildId = guild;
        Set = null;
    }

    public Game(Multiversus.Set set, IUserMessage message)
    {
        var team1Bans = set.CurrentGame != 1 ? set.WonLast == 1 : Random.RandomBool();
        if (team1Bans)
        {
            Team1 = set.Team1;
            Team2 = set.Team2;
        }
        else
        {
            Team1 = set.Team2;
            Team2 = set.Team1;
        }

        Message = message;
        GuildId = set.Channel.GuildId;
        Set = set;
    }

    public IUserMessage Message { get; set; }


    public Guild gld => Handler.Guild.GetDiscordOrMake(GuildId);

    public Dictionary<string, List<string>> Maps => gld.Maps;

    public bool OnTeam(SocketUser user, Team team)
    {
        foreach (var teamMember in team.Users)
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

    public Message.MonarkMessage BuildSelectPhase()
    {
        var msg = new Message.MonarkMessage();
        msg.Components = new ComponentBuilder()
            .WithSelectMenus("bans", BuildBanSelectOptions(), "Pick a map to select")
            .WithButton("Restart Map Selection", "restart", ButtonStyle.Danger).Build();
        msg.AddEmbed(new EmbedBuilder().WithTitle("Please select a map to play").AddField($"{Team1.Name}",
            $"Map Banned:\n{BlockedMap}", true).AddField($"{Team2.Name} (Your Turn)",
            "Map Selected", true));

        if (CoinflipWinner != null)
        {
            string coinflipText = Team1.Name + " won the coinflip.";
            msg.AddEmbed(new EmbedBuilder().WithTitle(coinflipText));
        }


        return msg;
    }

    public Message.MonarkMessage BuildPoolPhase()
    {
        if (Maps.Count == 0) return "There are no map pools created";

        if (Maps.Count == 1) return BuildBanPhase(Maps.First().Value);

        var message = new Message.MonarkMessage();
        message.AddEmbed(new EmbedBuilder().WithTitle("Please select a map pool"));
        message.Components = new ComponentBuilder().WithSelectMenus("maps", BuildSelectOptions())
            .WithButton("Restart Map Selection", "restart", ButtonStyle.Danger).Build();

        if (CoinflipWinner != null)
        {
            string coinflipText = Team1.Name + " won the coinflip.";
            message.AddEmbed(new EmbedBuilder().WithTitle(coinflipText));
        }

        return message;
    }

    public Message.MonarkMessage BuildBanPhase(List<string> mapPool)
    {
        // Ensure that no link is made to the settings.
        MapPool = new List<string>(mapPool);

        var msg = new Message.MonarkMessage();
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
            $"{Team1.Name} (Your Turn)",
            $"Map Banned:\n{BlockedMap}", true).AddField($"{Team2.Name}",
            "Map selected:", true));

        if (CoinflipWinner != null)
        {
            string coinflipText = Team1.Name + " won the coinflip.";
            msg.AddEmbed(new EmbedBuilder().WithTitle(coinflipText));
        }

        return msg;
    }

    public Message.MonarkMessage BuildDonePhase()
    {
        var msg = new Message.MonarkMessage();
        var components = new ComponentBuilder();
        if (Set == null)
        {
            components.WithButton("New Game", "restart").WithButton("End set", "end", ButtonStyle.Danger);
        }
        else
        {
            components.WithButton("Undo Selection (Only for mistakes)", "restart", ButtonStyle.Danger);
            msg.AddEmbed(new EmbedBuilder().WithTitle(
                "To start the next game in the set, please update the score on the start.gg website."));
        }

        msg.Components = components.Build();

        msg.AddEmbed(new EmbedBuilder().WithDescription("Done").AddField($"{Team1.Name}",
            $"Maps Banned:\n{BlockedMap}", true).AddField($"{Team2.Name}",
            $"Maps Selected:\n{SelectedMap}", true));

        if (CoinflipWinner != null)
        {
            string coinflipText = Team1.Name + " won the coinflip.";
            msg.AddEmbed(new EmbedBuilder().WithTitle(coinflipText));
        }

        return msg;
    }

    public List<SelectMenuOptionBuilder> BuildBanSelectOptions()
    {
        var options = new List<SelectMenuOptionBuilder>();

        foreach (var a in MapPool)
            options.Add(new SelectMenuOptionBuilder(a, a));

        return options;
    }

    public Message.MonarkMessage BuildFirst()
    {
        try
        {
            var message = new Message.MonarkMessage();
            message.AddEmbed(new EmbedBuilder().WithTitle("Please select the state of the game."));
            message.Components =
                new ComponentBuilder()
                    .WithButton("First game of the set", "coinflip")
                    .WithButton($"{Team1.Name} won last game", "wonlast1", ButtonStyle.Success)
                    .WithButton($"{Team2.Name} won last game", "wonlast2", ButtonStyle.Success)
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
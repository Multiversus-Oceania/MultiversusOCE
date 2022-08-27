using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BasicBot.Handler;
using BasicBot.MonarkTypes;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace BasicBot.Multiversus;

public class InProgressBoard
{
    public static Dictionary<string, IUserMessage> Messages = new();

    public static async Task UpdateBoards()
    {
        List<Task> tasks = new();
        foreach (var runningEvent in Multiversus.Sets.Keys)
        {
            tasks.Add(UpdateBoard(runningEvent));
        }

        await Task.WhenAll(tasks);
    }

    public static async Task UpdateBoard(string id)
    {
        try
        {
            var sets = Multiversus.Sets[id];


            SocketGuild guild = null;
            if (sets.Values.Count > 0)
            {
                guild = Program.discordClient.GetGuild(sets.Values.First().Channel.GuildId);
                var gld = await Guild.GetDiscordOrMake(guild.Id);

                var msg = new Message.MonarkMessage();

                var setsEmbed = new EmbedBuilder()
                {
                    Title = "Sets In Progress"
                };

                foreach (var set in sets.Values)
                {
                    setsEmbed.AddField(set.Team1.Name + " vs " + set.Team2.Name,
                        set.Wins[0] + " - " + set.Wins[1],
                        true);
                }

                msg.AddEmbed(setsEmbed);
                if (gld.Channels.ContainsKey(Settings.Guild.ChannelType.TournamentBoard) &&
                    guild.Channels.Count(x => x.Id == gld.Channels[Settings.Guild.ChannelType.TournamentBoard]) > 0)
                {
                    if (Messages.ContainsKey(id))
                    {
                        // Update
                        var message = Messages[id];
                        if (message != null)
                        {
                            await msg.UpdateMessage(message);
                        }
                        else
                        {
                            Messages.Remove(id);
                        }
                    }
                    else
                    {
                        // Send new message
                        var channel =
                            Program.discordClient.GetChannel(gld.Channels[Settings.Guild.ChannelType.TournamentBoard])
                                as SocketTextChannel;
                        var message = await msg.SendMessage(channel);
                        Messages.Add(id, message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonConvert.SerializeObject(ex, Formatting.Indented));
        }
    }
}
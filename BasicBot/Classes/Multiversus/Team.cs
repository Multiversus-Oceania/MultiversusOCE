using System;
using System.Collections.Generic;
using BasicBot.GraphQL.SetsAndLinkedAccounts;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace BasicBot.Multiversus;

public class Team
{
    public string Id;
    public List<SocketGuildUser> Users;
    public string Name;

    private void Setup(List<SocketGuildUser> socketUsers, string name = null, string id = "")
    {
        if (socketUsers == null)
        {
            throw new ArgumentNullException("SocketUsers is null.");
        }

        if (socketUsers.Count == 0)
        {
            throw new Exception("No users provided for team.");
        }

        if (name == null)
        {
            name = "";
            foreach (var user in socketUsers)
            {
                if (name != "") name += "/";
                name += user.DisplayName;
            }
        }

        Users = socketUsers;
        Name = name;
        Id = id;
    }

    public Team(List<SocketGuildUser> socketUsers, string name = null)
    {
        Setup(socketUsers, name);
    }

    public Team(SocketGuildUser user, string name = null)
    {
        Setup(new List<SocketGuildUser>() { user }, name);
    }

    public Team(Entrant entrant, SocketGuild guild)
    {
        List<SocketGuildUser> users = new List<SocketGuildUser>();
        // Get the discord users out of the set info.
        foreach (var participant in entrant.Participants)
        {
            try
            {
                if (participant == null) continue;
                
                foreach (var connection in participant.RequiredConnections)
                {
                    if (connection == null) continue;
                    if (connection.Type != TypeEnum.Discord) continue;

                    if (ulong.TryParse(connection.ExternalId, out var id))
                    {
                        SocketGuildUser user = guild.GetUser(id);

                        if (user == null) continue;

                        users.Add(user);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(JsonConvert.SerializeObject(participant, Formatting.Indented));
            }
        }
        
        Setup(users, entrant.Name, entrant.Id);
    }
}
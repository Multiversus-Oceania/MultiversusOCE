using System;
using System.Collections.Generic;
using BasicBot.GraphQL.SetsAndLinkedAccounts;
using Discord.WebSocket;

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
            foreach (var connection in participant.RequiredConnections)
            {
                if (connection.Type != TypeEnum.Discord) continue;

                if (ulong.TryParse(connection.ExternalId, out var id))
                {
                    SocketGuildUser user = guild.GetUser(id);
                    users.Add(user);
                }
            }
        }

        Setup(users, entrant.Name, entrant.Id);
    }
}
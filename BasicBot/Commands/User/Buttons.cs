using System.Linq;
using System.Threading.Tasks;
using BasicBot.Handler;
using Discord.Interactions;
using Discord.WebSocket;
using static BasicBot.Handler.Multiversus;
using static BasicBot.MonarkTypes.Message;

namespace BasicBot.Commands
{
    public class ButtonCommand : InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>
    {
        [ComponentInteraction("bans:*")]
        public async Task RoleSelection(string id, string[] selected)
        {
            await DeferAsync(true);
            if (GetThing(Context.Interaction.Message.Id) is gamething game)
            {
                MonarkMessage msg = "Bugged";

                if (await game.SelectMap(Context.User, selected.First()))
                {
                    //msg = "Added Ban";
                }
                else
                {
                    msg = "Its not your turn";
                    await msg.SendMessage(Context.Interaction);
                }
            }
        }

        [ComponentInteraction("maps:*")]
        public async Task MapSelection(string id, string[] selected)
        {
            var gld = Guild.GetDiscordOrMake(Context.Guild);
            if (!gld.Maps.ContainsKey(selected.First()))
            {
                await Context.Interaction.RespondAsync("Failed to find", ephemeral: true);
                return;
            }

            await DeferAsync(true);

            if (GetThing(Context.Interaction.Message.Id) is gamething game)
                if (game.OnTeam(Context.User, game.Team1) || game.OnTeam(Context.User, game.Team2))
                    await game.BuildBanPhase(gld.Maps[selected.First()]).UpdateMessage(game.Message);
        }

        [ComponentInteraction("wonlast*")]
        public async Task WonLastButton()
        {
            var gld = Guild.GetDiscordOrMake(Context.Guild);

            await DeferAsync(true);

            if (GetThing(Context.Interaction.Message.Id) is gamething game)
            {
                if (Context.Interaction.Data.CustomId == "wonlast2")
                {
                    (game.Team1, game.Team2) = (game.Team2, game.Team1);
                }

                await game.BuildPoolPhase().UpdateMessage(game.Message);
            }
        }

        [ComponentInteraction("coinflip")]
        public async Task CoinflipButton()
        {
            var gld = Guild.GetDiscordOrMake(Context.Guild);

            await DeferAsync(true);

            if (GetThing(Context.Interaction.Message.Id) is gamething game)
            {
                if (Random.RandomBool())
                {
                    (game.Team1, game.Team2) = (game.Team2, game.Team1);
                    (game.Team1Name, game.Team2Name) = (game.Team2Name, game.Team1Name);
                }

                await game.BuildPoolPhase().UpdateMessage(game.Message);
            }
        }

        [ComponentInteraction("end")]
        public async Task End()
        {
            await DeferAsync(true);

            if (GetThing(Context.Interaction.Message.Id) is gamething game)
            {
                things.Remove(game.Message.Id);
                foreach (var channel in Context.Guild.Channels)
                {
                    if (channel.Id == Context.Interaction.ChannelId)
                    {
                        await channel.DeleteAsync();
                    }
                }
            }
        }

        [ComponentInteraction("next")]
        public async Task Next()
        {
            await DeferAsync(true);

            if (GetThing(Context.Interaction.Message.Id) is gamething game)
            {
                game.Set.NextGame(0);
            }
        }

        [ComponentInteraction("restart")]
        public async Task Restart()
        {
            await DeferAsync(true);

            if (GetThing(Context.Interaction.Message.Id) is gamething game)
            {
                game.BlockedMap = "";
                game.SelectedMap = "";

                await game.BuildFirst().UpdateMessage(game.Message);
            }
        }
    }
}
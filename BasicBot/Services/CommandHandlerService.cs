using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using BasicBot.Commands;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using static BasicBot.Handler.Settings;

namespace BasicBot.Services
{
    public class CommandHandlerService
    {
        private readonly DiscordSocketClient discord;
        private readonly CommandService commands;
        private readonly IServiceProvider provider;
        public static InteractionService interactionService;

        public async Task InitializeAsync()
        {
            interactionService.AddTypeConverter<TimeSpan>(new TimeSpanConverter());
            interactionService.AddTypeConverter<List<int>>(new ListIntsConverter());
            interactionService.AddTypeConverter<List<string>>(new ListstringConverter());
            interactionService.AddTypeConverter<ulong>(new UlongConverter());
            interactionService.AddTypeConverter<SocketGuild>(new GuildConverter());
            commands.AddTypeReader(typeof(TimeSpan), new TimeSpanTypeReader());
            await commands.AddModulesAsync(Assembly.GetEntryAssembly(), provider);
            await interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), provider);

            //commands.add
        }

        public CommandHandlerService(IServiceProvider _provider, DiscordSocketClient _discord, CommandService _commands)
        {
            provider = _provider;
            discord = _discord;
            commands = _commands;
            interactionService = new InteractionService(discord, new InteractionServiceConfig
                {
                    ThrowOnError = true
                }
            );

            discord.MessageReceived += Discord_MessageReceived;
            discord.Log += Discord_Log;
            discord.Ready += Client_Ready;
            discord.InteractionCreated += Client_InteractionCreated;
            discord.UserJoined += Discord_UserJoined;
            discord.GuildAvailable += Discord_GuildAvailable;
            // discord.ThreadCreated += Discord_ThreadCreated;
            // discord.ThreadUpdated += Discord_ThreadUpdated;
        }

        private Task Discord_ThreadUpdated(Cacheable<SocketThreadChannel, ulong> arg1, SocketThreadChannel arg2)
        {
            throw new NotImplementedException();
        }

        private Task Discord_ThreadCreated(SocketThreadChannel arg)
        {
            throw new NotImplementedException();
        }

        private async Task Discord_GuildAvailable(SocketGuild arg)
        {
            try
            {
                await interactionService.RegisterCommandsToGuildAsync(arg.Id);
            }
            catch (Exception ex)
            {
            }
        }

        private async Task Discord_UserJoined(SocketGuildUser user)
        {
        }

        private async Task Client_InteractionCreated(SocketInteraction arg)
        {
            switch (arg)
            {
                case SocketSlashCommand:
                    var SlashContext =
                        new SocketInteractionContext<SocketSlashCommand>(discord, arg as SocketSlashCommand);
                    await interactionService.ExecuteCommandAsync(SlashContext, provider);
                    break;
                case SocketMessageCommand:
                    var MessageContext =
                        new SocketInteractionContext<SocketMessageCommand>(discord, arg as SocketMessageCommand);
                    await interactionService.ExecuteCommandAsync(MessageContext, provider);
                    break;
                case SocketMessageComponent:
                    var _MessageContext =
                        new SocketInteractionContext<SocketMessageComponent>(discord, arg as SocketMessageComponent);
                    await interactionService.ExecuteCommandAsync(_MessageContext, provider);
                    break;

                case SocketUserCommand:
                    var UserContext =
                        new SocketInteractionContext<SocketUserCommand>(discord, arg as SocketUserCommand);
                    await interactionService.ExecuteCommandAsync(UserContext, provider);
                    break;
                case SocketModal:
                    var ModalContext = new SocketInteractionContext<SocketModal>(discord, arg as SocketModal);
                    await interactionService.ExecuteCommandAsync(ModalContext, provider);
                    break;

                default:
                    var context = new SocketInteractionContext(discord, arg);

                    var a = await interactionService.ExecuteCommandAsync(context, provider);

                    break;
            }
        }

        public async Task Client_Ready()
        {
            //interactionService.

            //_ = interactionService.RegisterCommandsToGuildAsync(537887361292304385, true);
            //_ = interactionService.RegisterCommandsToGuildAsync(362611092893073408, true);
            //_ = interactionService.RegisterCommandsToGuildAsync(362606684352413706, true);
            //_ = interactionService.RegisterCommandsToGuildAsync(392914324839989248, true);

            //await discord.BulkOverwriteGlobalApplicationCommandsAsync(new ApplicationCommandProperties());

            await discord.SetGameAsync("just a bot", type: ActivityType.Playing);
        }

        private Task Discord_Log(LogMessage arg)
        {
            Console.WriteLine(arg.Message);
            return Task.CompletedTask;
        }

        private async Task Discord_MessageReceived(SocketMessage socketMessage)
        {
            if (socketMessage.Author.IsBot)
            {
                return;
            }

            if (socketMessage.Channel is SocketGuildChannel)
            {
                //if (await ModMailHandlerService.SendMessageStaff(socketMessage))
                {
                    //return;
                }
            }


            if (socketMessage.Channel is IDMChannel chnl)
            {
                //await ModMailHandlerService.SendMessageUser(socketMessage);
            }

            else if (socketMessage is IUserMessage message)
            {
                var context = new CommandContext(discord, message);
                var botPrefx = GetSettings().BotPrefix;
                var argPos = 0;

                if (message.HasStringPrefix(botPrefx, ref argPos, StringComparison.CurrentCultureIgnoreCase))
                {
                    var result = await commands.ExecuteAsync(context, argPos, provider);
                    if (result.Error != null)
                    {
                        //DO STUFF HERE
                    }
                }
            }
        }
    }
}
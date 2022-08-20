using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BasicBot.Handler;
using Discord;
using Discord.Webhook;
using Discord.WebSocket;

namespace BasicBot.MonarkTypes
{
    public static class Message
    {
        public static string GetStickerUrl(this IStickerItem item)
        {
            return CDN.GetStickerUrl(item.Id, item.Format);
        }

        private static string FormatToExtension(this StickerFormatType format)
        {
            return format switch
            {
                StickerFormatType.None or StickerFormatType.Png
                    or StickerFormatType
                        .Apng => "png", // In the case of the Sticker endpoint, the sticker will be available as PNG if its format_type is PNG or APNG, and as Lottie if its format_type is LOTTIE.
                StickerFormatType.Lottie => "lottie",
                _ => throw new ArgumentException(nameof(format))
            };
        }

        public class MonarkMessage
        {
            public string Content;
            public List<string> Errors = new();
            public MessageReference Reference;
            public List<Embed> Embeds;
            public List<MonarkAttachment> Attachments;
            public MessageComponent Components = null;

            public static implicit operator MonarkMessage(string str)
            {
                var msg = new MonarkMessage();
                msg.Content = str;

                return msg;
            }

            public static implicit operator MonarkMessage(Embed embed)
            {
                var msg = new MonarkMessage();
                msg.Embeds = new List<Embed> { embed };

                return msg;
            }

            private Embed[] MakeEmbeds()
            {
                if (Embeds == null)
                    return null;

                return Embeds.ToArray();
            }

            public async Task<List<FileAttachment>> BuildAttachment()
            {
                if (Attachments == null)
                {
                    return null;
                }

                var Tasks = Attachments.Select(x => x.Build()).ToList();
                await Task.WhenAll(Tasks);

                var results = Tasks.Where(
                    x =>
                        x.IsCompletedSuccessfully).Select(x =>
                    x.Result);

                if (results.Count() > 0)
                    return results.ToList();


                return null;
            }


            private void SetupAttachments(List<IAttachment> attachments, List<IStickerItem> sticker)
            {
                List<MonarkAttachment> att = new();

                foreach (var a in attachments)
                {
                    //if file is less then 10 MB
                    if (a.Size < 10 * 1000 * 1000)
                    {
                        att.Add(new MonarkAttachment(a.Url, a.Filename));
                    }
                    else
                    {
                        Errors.Add("Failed to upload");
                    }
                }

                foreach (var a in sticker)
                {
                    att.Add(new MonarkAttachment(a.GetStickerUrl(), $"a.Name.{a.Format.FormatToExtension()}"));
                }

                if (att.Count > 0)
                {
                    Attachments = att;
                }
            }

            public MonarkMessage(IMessage Msg, bool CleanContent = true, bool copyRefrence = false)
            {
                if (CleanContent)
                {
                    Content = Msg.CleanContent;
                }
                else
                {
                    Content = Msg.Content;
                }

                if (copyRefrence)
                {
                    Reference = Msg.Reference;
                }

                SetupAttachments(Msg.Attachments.ToList(), Msg.Stickers.ToList());
            }

            public MonarkMessage(string content)
            {
                Content = content;
            }

            public MonarkMessage(string content, MonarkEmbed embed)
            {
                Content = content;
                Embeds = new List<Embed> { embed };
            }

            public MonarkMessage()
            {
            }

            public static Dictionary<ulong, IDMChannel> UserDmsChannels = new();

            public async Task<IUserMessage> SendMessageDM(IUser user)
            {
                return await SendMessageDM(user.Id);
            }

            public async Task<IUserMessage> SendMessageDM(ulong id)
            {
                if (UserDmsChannels.ContainsKey(id))
                {
                    return await SendMessage(UserDmsChannels[id]);
                }

                if (Program.discordClient.GetUser(id) is SocketUser user)
                {
                    var chnl = await user.CreateDMChannelAsync();
                    UserDmsChannels[id] = chnl;
                    return await SendMessage(chnl);
                }

                return null;
            }

            public async Task<IUserMessage> SendMessage(IMessageChannel chnl)
            {
                if (await BuildAttachment() is IEnumerable<FileAttachment> Acc)
                {
                    try
                    {
                        return await chnl.SendFilesAsync(Acc, Content, false, null, null, AllowedMentions.None,
                            Reference, Components, null, MakeEmbeds());
                    }
                    catch
                    {
                        return null;
                    }
                }
                //does not have attachments

                try
                {
                    return await chnl.SendMessageAsync(Content, false, null, null, AllowedMentions.None, Reference,
                        Components, null, MakeEmbeds());
                }
                catch (Exception exe)
                {
                    return null;
                }
            }

            public async Task UpdateMessage(IMessageChannel chnl, ulong msgId)
            {
                try
                {
                    await chnl.ModifyMessageAsync(msgId, x => x = UpdateProperties(x));
                }
                catch (Exception exe)
                {
                }
            }

            public async Task UpdateMessage(IUserMessage msg)
            {
                try
                {
                    await msg.ModifyAsync(x => x = UpdateProperties(x));
                }
                catch (Exception exe)
                {
                }
            }

            public async Task UpdateMessage(SocketInteraction msg)
            {
                try
                {
                    await msg.ModifyOriginalResponseAsync(x => x = UpdateProperties(x));
                    ;
                }
                catch (Exception exe)
                {
                }
            }

            private MessageProperties UpdateProperties(MessageProperties prop)
            {
                prop.Content = Content;
                prop.Components = Components;

                if (Embeds != null && Embeds.Count > 0)
                {
                    prop.Embeds = Embeds.ToArray();
                }
                else
                {
                    prop.Embeds = null;
                }

                return prop;
            }

            public async Task SendMessage(SocketInteraction interaction, bool ephemeral = true)
            {
                if (await BuildAttachment() is IEnumerable<FileAttachment> Acc)
                {
                    try
                    {
                        await interaction.SendMsgFiles(Acc, Content, MakeEmbeds(), false, ephemeral,
                            AllowedMentions.None, Components);
                        return;
                    }
                    catch
                    {
                        return;
                    }
                }
                //does not have attachments

                try
                {
                    await interaction.SendMsg(Content, MakeEmbeds(), false, ephemeral, AllowedMentions.None,
                        Components);
                }
                catch
                {
                }
            }

            public async Task<ulong> SendMessage(DiscordWebhookClient client, WebhookUser user, ulong? threadId)
            {
                if (Reference != null)
                {
                    var embeds = new List<Embed>();
                    embeds.Add(MonarkEmbed.CreateLinkEmbed("Reply to",
                        $"https://discord.com/channels/{Reference.GuildId}/{Reference.ChannelId}/{Reference.MessageId}"));

                    if (Embeds != null)
                    {
                        embeds.AddRange(Embeds);
                    }

                    Embeds = embeds;
                }

                //has attachments
                if (await BuildAttachment() is IEnumerable<FileAttachment> Acc)
                {
                    try
                    {
                        return await client.SendFilesAsync(Acc, Content, false, MakeEmbeds(), user.UserName,
                            user.UrlProfile, null, null, null, threadId: threadId);
                    }
                    catch (Exception exe)
                    {
                        return 0;
                    }
                }
                //does not have attachments

                try
                {
                    return await client.SendMessageAsync(Content, false, MakeEmbeds(), user.UserName, user.UrlProfile,
                        null, null, null, threadId: threadId);
                }
                catch (Exception exe)
                {
                    return 0;
                }
            }

            public void AddEmbed(EmbedBuilder embedBuilder)
            {
                AddEmbed(embedBuilder.Build());
            }

            public void AddEmbed(Embed embed)
            {
                if (Embeds == null)
                    Embeds = new List<Embed>();

                Embeds.Add(embed);
            }
        }


        public class MonarkAttachment
        {
            public string Url = "";
            public string Name = "name";

            public static implicit operator MonarkAttachment(Attachment attachment)
            {
                return new MonarkAttachment(attachment.Url, attachment.Filename);
            }

            public MonarkAttachment(string link, string name)
            {
                Url = link;
                Name = name;
            }

            public MonarkAttachment()
            {
            }


            public async Task<FileAttachment> Build()
            {
                var webClient = new WebClient();

                var data = await webClient.DownloadDataTaskAsync(Url);

                var mem = new MemoryStream(data);
                return new FileAttachment(mem, Name);
            }
        }


        public class MonarkEmbed
        {
            public EmbedBuilder Embed = new();

            public static implicit operator MonarkEmbed(string content)
            {
                return new MonarkEmbed(content);
            }

            public static implicit operator Embed(MonarkEmbed em)
            {
                return em.Embed.Build();
            }

            public static implicit operator Optional<Embed>(MonarkEmbed em)
            {
                return em.Embed.Build();
            }

            public static implicit operator EmbedBuilder(MonarkEmbed em)
            {
                return em.Embed;
            }

            public MonarkEmbed(string content)
            {
                Embed.Description = content;
            }

            public static MonarkEmbed CreateLinkEmbed(string title, string link)
            {
                var embed = new MonarkEmbed();

                embed.Embed.WithDescription($"[{title}]({link})");

                return embed;
            }


            public MonarkEmbed()
            {
            }
        }


        public class WebhookUser
        {
            public string UserName = "Monark";
            public string UrlProfile = "https://bookstr.com/wp-content/uploads/2022/03/The-Batman.jpeg";

            public static implicit operator WebhookUser(SocketUser user)
            {
                return new WebhookUser(user.Username, user.GetAvatarUrl());
            }


            public WebhookUser(string name, string url)
            {
                UserName = name;
                UrlProfile = url;
            }

            public WebhookUser()
            {
            }
        }
    }
}
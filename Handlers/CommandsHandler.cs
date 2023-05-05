using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using CharacterAI_Discord_Bot.Service;
using CharacterAI_Discord_Bot.Models;
using CharacterAI;
using System.Text;
using System.Threading.Channels;
using static System.Net.Mime.MediaTypeNames;

namespace CharacterAI_Discord_Bot.Handlers
{
    public class CommandsHandler : HandlerService
    {
        internal Integration CurrentIntegration { get; }
        internal int ReplyChance { get; set; }
        internal List<ulong> BlackList { get; set; } = new();
        internal List<Models.Channel> Channels { get; set; } = new();
        internal LastSearchQuery? LastSearch { get; set; }
        internal Dictionary<ulong, int> HuntedUsers { get; set; } = new(); // user id : reply chance

        private readonly Dictionary<ulong, int[]> _userMsgCount = new();
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;
        private readonly CommandService _commands;

        public CommandsHandler(IServiceProvider services)
        {
            CurrentIntegration = new(BotConfig.UserToken);

            _services = services;
            _commands = services.GetRequiredService<CommandService>();
            _client = services.GetRequiredService<DiscordSocketClient>();

            ReplyChance = BotConfig.ReplyChance;

            _client.MessageReceived += HandleMessage;
            _client.ReactionAdded += HandleReaction;
            _client.ReactionRemoved += HandleReaction;
            _client.ButtonExecuted += HandleButton;

            _ = Task.Run(HandleChannelsTimeout);
        }

        public async Task InitializeChannels()
        {
            Console.WriteLine("Initializing channels!!!");
            foreach (var guild in _client.Guilds)
            {
                foreach (var discrodChannel in guild.TextChannels)
                {
                    var permissions = guild.CurrentUser.GetPermissions(discrodChannel);
                    if (!permissions.ViewChannel && CurrentIntegration.CurrentCharacter.IsEmpty)
                    {
                        continue; // если бот не может видеть канал, то пропускаем его
                    }

                    var botChannel = Channels.Find(CurrentIntegration => CurrentIntegration.Id == discrodChannel.Id);
                    bool isPrivate = discrodChannel.Name.StartsWith("private");
                    if (botChannel is null)
                    {
                        continue;
                        if (isPrivate) return;

                        string? historyId = null;
                        historyId ??= CurrentIntegration.Chats[0];

                        botChannel = new Models.Channel(discrodChannel.Id, discrodChannel.Users.FirstOrDefault().Id, historyId, CurrentIntegration.CurrentCharacter.Id!);

                        Channels.Add(botChannel);
                        SaveData(channels: Channels);

                    }

                    botChannel.LastMessageTime = DateTime.Now;
                    botChannel.MessageTimeoutMins = BotConfig.MessageTimeoutMins;
                    botChannel.MessagesTextBuffer = new StringBuilder();
                }
            }

            Console.WriteLine("InitializeChannels executed successfully!!!");
        }
        private async Task HandleMessage(SocketMessage rawMsg)
        {
            var authorId = rawMsg.Author.Id;
            if (rawMsg is not SocketUserMessage message || authorId == _client.CurrentUser.Id)
                return;

            var context = new SocketCommandContext(_client, message);
            bool isPrivate = context.Channel.Name.StartsWith("private");
            var cI = CurrentIntegration;
            var currentBotChannel = Channels.Find(c => c.Id == context.Channel.Id);

            int argPos = 0;
            var randomNumber = new Random();
            string[] prefixes = BotConfig.BotPrefixes;

            bool isDM = context.Guild is null;
            bool hasMention = isDM || message.Content.Contains($"<@{_client.CurrentUser.Id}>");
            bool hasPrefix = hasMention || prefixes.Any(p => message.HasStringPrefix(p, ref argPos));
            bool hasReply = hasPrefix || message.ReferencedMessage?.Author.Id == _client.CurrentUser.Id; // IT'S SO FUCKING BIG UUUGHH!
            bool randomReply = hasReply || (ReplyChance >= randomNumber.Next(100) + 1);
            bool userIsHunted = randomReply || (HuntedUsers.ContainsKey(authorId) && HuntedUsers[authorId] >= randomNumber.Next(100) + 1);

            if (!isDM && !CurrentIntegration.CurrentCharacter.IsEmpty)
            {
                if (currentBotChannel is null)
                {
                    if (isPrivate) return;

                    string? historyId = null;
                    historyId = await cI.CreateNewChatAsync();
                    historyId ??= cI.Chats[0];

                    currentBotChannel = new Models.Channel(context.Channel.Id, context.User.Id, historyId, cI.CurrentCharacter.Id!);

                    Channels.Add(currentBotChannel);
                    SaveData(channels: Channels);
                }

                currentBotChannel.LastMessageTime = DateTime.Now;
                currentBotChannel.IncactiveMessageCount = 0;
                currentBotChannel.MessageTimeoutMins = BotConfig.MessageTimeoutMins;

                string text = RemoveMention(context.Message.Content);

                // Prepare call data
                int amode = currentBotChannel.Data.AudienceMode;
                if (amode == 1 || amode == 3)
                    text = AddUsername(text, _client.CurrentUser.Id, CurrentIntegration.CurrentCharacter.Name, context.Message);
                if (amode == 2 || amode == 3)
                    text = AddQuote(text, _client.CurrentUser.Id, CurrentIntegration.CurrentCharacter.Name, context.Message);

                if (currentBotChannel.MessagesTextBuffer is null)
                {
                    currentBotChannel.MessagesTextBuffer = new StringBuilder(text);
                }
                lock (currentBotChannel.MessagesTextBuffer)
                {
                    if (currentBotChannel.MessagesTextBuffer.Length == 0)

                        currentBotChannel.MessagesTextBuffer.Append(text);
                    else
                        currentBotChannel.MessagesTextBuffer.Append("\n\n" + text);
                }
            }

            bool messageBufferOwerflow = currentBotChannel?.MessagesTextBuffer?.Length > BotConfig.MessagesBufferLength;

            if (isDM || hasMention || hasPrefix || hasReply || userIsHunted || randomReply || messageBufferOwerflow)
            {
                // Update messages-per-minute counter.
                // If user has exceeded rate limit, or if message is a DM and these are disabled - return
                if ((isDM && !BotConfig.DMenabled) || UserIsBanned(context)) return;

                // Try to execute command
                var cmdResponse = await _commands.ExecuteAsync(context, argPos, _services);
                // If command was found and executed, return
                if (cmdResponse.IsSuccess) return;
                // If command was found but failed to execute, return
                if (cmdResponse.ErrorReason != "Unknown command.")
                {
                    string text = $"⚠ Failed to execute command: {cmdResponse.ErrorReason} ({cmdResponse.Error})";
                    if (isDM) text = "*Note: some commands are not intended to be called from DMs*\n" + text;

                    await message.ReplyAsync(text).ConfigureAwait(false);
                    return;
                }

                // If command was not found, perform character call
                if (cI.CurrentCharacter.IsEmpty)
                {
                    await context.Message.ReplyAsync("⚠ Set a character first").ConfigureAwait(false);
                    return;
                }

                if (currentBotChannel is null)
                {
                    if (isPrivate) return;

                    string? historyId = null;
                    historyId = await cI.CreateNewChatAsync();
                    historyId ??= cI.Chats[0];

                    currentBotChannel = new Models.Channel(context.Channel.Id, context.User.Id, historyId, cI.CurrentCharacter.Id!);

                    Channels.Add(currentBotChannel);
                    SaveData(channels: Channels);
                }

                if (currentBotChannel.Data.SkipMessages > 0)
                    currentBotChannel.Data.SkipMessages--;
                else
                    using (message.Channel.EnterTypingState())
                        _ = TryToCallCharacterAsync(context, currentBotChannel, isDM || isPrivate);
            }

        }

        private async Task HandleReaction(Cacheable<IUserMessage, ulong> rawMessage, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            var message = await rawMessage.DownloadAsync();
            var currentChannel = Channels.Find(c => c.Id == message.Channel.Id);
            if (currentChannel is null) return;

            if (currentChannel.Data.LastCall is null || rawMessage.Id != currentChannel.Data.LastCharacterCallMsgId)
                return;

            var user = reaction.User.Value as SocketUser;
            if (user!.IsBot || user.Id != message.ReferencedMessage.Author.Id)
                return;

            if (reaction.Emote.Name == new Emoji("\u2B05").Name && currentChannel.Data.LastCall!.CurrentReplyIndex > 0)
            {   // left arrow
                currentChannel.Data.LastCall!.CurrentReplyIndex--;
                _ = UpdateMessageAsync(message, currentChannel);

                return;
            }
            if (reaction.Emote.Name == new Emoji("\u27A1").Name)
            {   // right arrow
                currentChannel.Data.LastCall!.CurrentReplyIndex++;
                _ = UpdateMessageAsync(message, currentChannel);

                return;
            }
        }

        // Navigate in search modal
        private async Task HandleButton(SocketMessageComponent component)
        {
            if (LastSearch is null) return;

            var context = new SocketCommandContext(_client, component.Message);
            var refMessage = await context.Message.Channel.GetMessageAsync(context.Message.Reference!.MessageId.Value);
            bool notAuthor = component.User.Id != refMessage.Author.Id;
            bool noPages = LastSearch!.Response.IsEmpty;
            if (notAuthor || UserIsBanned(context) || noPages) return;

            int tail = LastSearch!.Response!.Characters!.Count - (LastSearch.CurrentPage - 1) * 10;
            int maxRow = tail > 10 ? 10 : tail;

            switch (component.Data.CustomId)
            { // looks like shit...
                case "up":
                    if (LastSearch.CurrentRow == 1)
                        LastSearch.CurrentRow = maxRow;
                    else
                        LastSearch.CurrentRow--;
                    break;
                case "down":
                    if (LastSearch.CurrentRow > maxRow)
                        LastSearch.CurrentRow = 1;
                    else
                        LastSearch.CurrentRow++;
                    break;
                case "left":
                    LastSearch.CurrentRow = 1;

                    if (LastSearch.CurrentPage == 1)
                        LastSearch.CurrentPage = LastSearch.Pages;
                    else
                        LastSearch.CurrentPage--;
                    break;
                case "right":
                    LastSearch.CurrentRow = 1;

                    if (LastSearch.CurrentPage == LastSearch.Pages)
                        LastSearch.CurrentPage = 1;
                    else
                        LastSearch.CurrentPage++;
                    break;
                case "select":
                    var refContext = new SocketCommandContext(_client, (SocketUserMessage)refMessage);

                    using (refContext.Message.Channel.EnterTypingState())
                    {
                        int index = (LastSearch.CurrentPage - 1) * 10 + LastSearch.CurrentRow - 1;
                        var character = LastSearch.Response!.Characters![index];
                        if (character.IsEmpty) return;

                        _ = CommandsService.SetCharacterAsync(character.Id!, this, refContext);
                        await component.UpdateAsync(c =>
                        {
                            var imageUrl = TryGetImage(character.AvatarUrlFull!).Result ?
                                            character.AvatarUrlFull : TryGetImage(character.AvatarUrlMini!).Result ?
                                                character.AvatarUrlMini : null;

                            string desc = $"{character.Description}\n\n" +
                                          $"*Original link: [Chat with {character.Name}](https://beta.character.ai/chat?char={character.Id})*";
                            c.Embed = new EmbedBuilder()
                            {
                                Title = $"✅ Selected - {character.Name}",
                                Description = desc,
                                ImageUrl = imageUrl,
                                Footer = new EmbedFooterBuilder().WithText($"Created by {character.Author}")
                            }.Build();
                            c.Components = null;
                        }).ConfigureAwait(false);
                    }
                    return;
                default:
                    return;
            }

            // Only if left/right/up/down is selected
            await component.UpdateAsync(c => c.Embed = BuildCharactersList(LastSearch))
                           .ConfigureAwait(false);
        }

        // Swipes
        private async Task UpdateMessageAsync(IUserMessage message, Models.Channel currentChannel)
        {
            if (currentChannel.Data.LastCall!.RepliesList.Count < currentChannel.Data.LastCall.CurrentReplyIndex + 1)
            {
                _ = message.ModifyAsync(msg => { msg.Content = $"( 🕓 Wait... )"; msg.AllowedMentions = AllowedMentions.None; });
                var historyId = currentChannel.Data.HistoryId;
                var parentMsgId = currentChannel.Data.LastCall.OriginalResponse.LastUserMsgId;
                var response = await CurrentIntegration.CallCharacterAsync(parentMsgId: parentMsgId, historyId: historyId);

                if (!response.IsSuccessful)
                {
                    _ = message.ModifyAsync(msg => { msg.Content = $"⚠ Somethinh went wrong!"; });
                    return;
                }
                currentChannel.Data.LastCall.RepliesList.AddRange(response.Replies);
            }
            var newReply = currentChannel.Data.LastCall.RepliesList[currentChannel.Data.LastCall.CurrentReplyIndex];
            currentChannel.Data.LastCall.CurrentPrimaryMsgId = newReply.Id;

            Embed? embed = null;
            if (newReply.HasImage && await TryGetImage(newReply.ImageRelPath!))
                embed = new EmbedBuilder().WithImageUrl(newReply.ImageRelPath).Build();

            _ = message.ModifyAsync(msg => { msg.Content = $"{newReply.Text}"; msg.Embed = embed; })
                .ConfigureAwait(false);
        }

        private async Task TryToCallCharacterAsync(SocketCommandContext context, Models.Channel currentChannel, bool isPrivate)
        {
            // Get last call and remove buttons from it
            if (currentChannel.Data.LastCharacterCallMsgId != 0)
            {
                var lastMessage = await context.Message.Channel.GetMessageAsync(currentChannel.Data.LastCharacterCallMsgId);
                _ = RemoveButtons(lastMessage);
            }

            string text = null;

            if (currentChannel.MessagesTextBuffer is not null)
            {
                lock (currentChannel.MessagesTextBuffer)
                {
                    if (currentChannel.MessagesTextBuffer.Length == 0)
                        return;
                    text = currentChannel.MessagesTextBuffer.ToString();
                    currentChannel.MessagesTextBuffer.Clear();
                }
            }
            else
            {
                text = RemoveMention(context.Message.Content);

                // Prepare call data
                int amode = currentChannel.Data.AudienceMode;
                if (amode == 1 || amode == 3)
                    text = AddUsername(text, _client.CurrentUser.Id, CurrentIntegration.CurrentCharacter.Name, context.Message);
                if (amode == 2 || amode == 3)
                    text = AddQuote(text, _client.CurrentUser.Id, CurrentIntegration.CurrentCharacter.Name, context.Message);
            }

            string? imgPath = null;
            if (context.Message.Attachments.Any())
            {   // Downloads first image from attachments and uploads it to server
                string url = context.Message.Attachments.First().Url;
                if (await TryDownloadImg(url, 10) is byte[] @img && await CurrentIntegration.UploadImageAsync(@img) is string @path)
                    imgPath = $"https://characterai.io/i/400/static/user/{@path}";
            }

            string historyId = currentChannel.Data.HistoryId;
            ulong? primaryMsgId = currentChannel.Data.LastCall?.CurrentPrimaryMsgId;

            // Send message to the character
            var response = await CurrentIntegration.CallCharacterAsync(text, imgPath, historyId, primaryMsgId);
            currentChannel.Data.LastCall = new(response);

            if (context.Message.Content.Contains($"<@{_client.CurrentUser.Id}>") || context.Message.ReferencedMessage?.Author.Id == _client.CurrentUser.Id)
            {
                // Alert with error message if call fails
                if (!response.IsSuccessful)
                {
                    await context.Message.ReplyAsync(response.ErrorReason).ConfigureAwait(false);
                    return;
                }

                // Take first character answer by default and reply with it
                var reply = currentChannel.Data.LastCall!.RepliesList.First();
                _ = Task.Run(async () => currentChannel.Data.LastCharacterCallMsgId = await ReplyOnMessage(context.Message, reply, isPrivate));
            }
            else
            {
                // Alert with error message if call fails
                if (!response.IsSuccessful)
                {
                    await context.Channel.SendMessageAsync(response.ErrorReason).ConfigureAwait(false);
                    return;
                }

                // Take first character answer by default and reply with it
                var reply = currentChannel.Data.LastCall!.RepliesList.First();
                _ = Task.Run(async () => currentChannel.Data.LastCharacterCallMsgId = await ReplyMessage(context.Message, reply, isPrivate));
            }
        }

        private bool UserIsBanned(SocketCommandContext context)
        {
            ulong currUserId = context.Message.Author.Id;
            if (context.Guild is not null && currUserId == context.Guild.OwnerId)
                return false;

            if (BlackList.Contains(currUserId)) return true;

            int currMinute = context.Message.CreatedAt.Minute + context.Message.CreatedAt.Hour * 60;

            // Start watching for user
            if (!_userMsgCount.ContainsKey(currUserId))
                _userMsgCount.Add(currUserId, new int[] { -1, 0 }); // current minute : count

            // Drop + update user stats if he replies in new minute
            if (_userMsgCount[currUserId][0] != currMinute)
            {
                _userMsgCount[currUserId][0] = currMinute;
                _userMsgCount[currUserId][1] = 0;
            }

            // Update messages count withing current minute
            _userMsgCount[currUserId][1]++;

            if (_userMsgCount[currUserId][1] == BotConfig.RateLimit - 1)
                context.Message.ReplyAsync($"⚠ Warning! If you proceed to call {context.Client.CurrentUser.Mention} " +
                                            "so fast, you'll be blocked from using it.");
            else if (_userMsgCount[currUserId][1] > BotConfig.RateLimit)
            {
                BlackList.Add(currUserId);
                _userMsgCount.Remove(currUserId);

                return true;
            }

            return false;
        }

        private async Task HandleChannelsTimeout()
        {
            int inactiveMessageCount = BotConfig.InactiveMessageCount;
            string deadChatMessage = BotConfig.DeadChatMessage;
            bool isIterationFailed = false;
            Console.WriteLine("deadChatMessage: " + deadChatMessage); ;

            while (true)
            {
                if (!isIterationFailed)
                    // Подождать 5 минут
                    await Task.Delay(TimeSpan.FromMinutes(BotConfig.MessageTimeoutMins));

                // Получить текущее время
                var currentTime = DateTime.Now;

                try
                {
                    // Проверить все каналы
                    foreach (var channel in Channels)
                    {
                        if (channel.MessagesTextBuffer is null || channel.IncactiveMessageCount > inactiveMessageCount) continue;
                        Console.WriteLine("Timuout: " + (currentTime - channel.LastMessageTime).TotalMinutes);
                        Console.WriteLine("channel.IncactiveMessageCount: " + channel.IncactiveMessageCount);
                        // Если канал был неактивен в течение MessageTimeoutMins минут
                        if ((currentTime - channel.LastMessageTime).TotalMinutes >= channel.MessageTimeoutMins)
                        {
                            int MessageTimeoutMins = channel.MessageTimeoutMins;
                            channel.MessageTimeoutMins *= 6;

                            if (channel.MessagesTextBuffer.Length == 0 && channel.IncactiveMessageCount == 0)
                            {
                                channel.IncactiveMessageCount++;
                                continue;
                            }

                            channel.IncactiveMessageCount++;

                            var discordChannel = await _client.GetChannelAsync(channel.Id) as SocketTextChannel;
                            string text = null;
                            lock (channel.MessagesTextBuffer)
                                if (channel.MessagesTextBuffer.Length > 0)
                                {
                                    text = channel.MessagesTextBuffer.ToString();
                                    channel.MessagesTextBuffer.Clear();
                                }
                                else
                                {
                                    text = $"{deadChatMessage} {MessageTimeoutMins} минут";
                                    Console.WriteLine(text);
                                }
                            // Вызвать метод CallCharacterAsync
                            using (discordChannel.EnterTypingState())
                            {
                                var response = await CurrentIntegration.CallCharacterAsync(text, null, channel.Data.HistoryId);
                                if (!response.IsSuccessful)
                                    await discordChannel.SendMessageAsync(response.ErrorReason).ConfigureAwait(false);
                                else
                                {
                                    await discordChannel.SendMessageAsync(response.Replies.First().Text).ConfigureAwait(false);
                                }
                            }
                            channel.LastMessageTime = DateTime.Now;
                        }
                    }
                    isIterationFailed = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    isIterationFailed = true;
                    continue;
                }
            }
        }


        public async Task InitializeAsync()
            => await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
    }
}
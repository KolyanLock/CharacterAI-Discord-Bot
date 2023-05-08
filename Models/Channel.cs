using CharacterAI_Discord_Bot.Service;
using Discord;
using System.Text;

namespace CharacterAI_Discord_Bot.Models
{
    internal class Channel
    {
        internal ulong Id { get; set; }
        internal ulong AuthorId { get; set; }
        internal DateTime LastMessageTime { get; set; }
        //internal Queue<IMessage> MessagesBuffer { get; set; }
        internal StringBuilder MessagesTextBuffer { get; set; }
        internal List<ulong> GuestsList { get; set; }
        internal CharacterDialogData Data { get; set; }
        internal Channel(ulong channelId, ulong authorId, string historyId, string characterId)
        {
            Id = channelId;
            AuthorId = authorId;
            GuestsList = new();
            Data = new(historyId, characterId);
            LastMessageTime = DateTime.Now;
        }
    }

    internal class CharacterDialogData : CommonService
    {
        internal string HistoryId { get; set; }
        internal string CharacterId { get; }
        internal int AudienceMode { get; set; }
        internal ulong LastCharacterCallMsgId { get; set; } // discord message id
        internal int SkipMessages { get; set; }
        internal LastCharacterCall? LastCall { get; set; }

        public CharacterDialogData(string historyId, string characterId)
        {
            HistoryId = historyId;
            CharacterId = characterId;
            AudienceMode = BotConfig.DefaultAudienceMode;
            SkipMessages = 0;
            LastCharacterCallMsgId = 0;
        }
    }
}

using System;

namespace Stormancer.Server.Chat
{
    [Flags]
    public enum DestinationType
    {
        Self = 1,
        Others = 2,
        All = 3,
    }

    public class ChatMsgReceivedContext
    {
        internal ChatMsgReceivedContext(ChatMessage message, ChatMessageDto messageDto)
        {
            Message = message;
            MessageDto = messageDto;
        }

        public ChatMessage Message { get; }
        public ChatMessageDto MessageDto { get; }
        public DestinationType Destination { get; set; }
    }    
}
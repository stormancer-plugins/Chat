using System;

namespace Stormancer.Server.Chat
{
    public class ChatShutdownContext
    {
        internal ChatShutdownContext(IObservable<ChatMessage> messages)
        {
            Messages = messages;
        }

        public IObservable<ChatMessage> Messages { get; }
    }
}
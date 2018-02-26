using System;

namespace Stormancer.Server.Chat
{
    public class ChatInitContext
    {
        internal ChatInitContext(IObservable<ChatMessage> messages)
        {
            Messages = messages;
        }

        public IObservable<ChatMessage> Messages { get; }
    }
}
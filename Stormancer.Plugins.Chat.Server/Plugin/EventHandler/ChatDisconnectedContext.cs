using System;

namespace Stormancer.Server.Chat
{
    public class ChatDisconnectedContext
    {
        internal ChatDisconnectedContext(string userId)
        {
            UserId = userId;
        }

        public string UserId { get; }
    }
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Server.Chat
{
    public interface IChatRepository
    {
        void AddMessageLog(ChatMessage messageLog);

        Task<List<ChatMessage>> SeekHistoryMessage(string channel, DateTime start, DateTime end);

        Task Flush();    
    }
}

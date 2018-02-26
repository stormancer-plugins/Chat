using System.Threading.Tasks;
using Stormancer;
using System.Collections.Generic;
using System;

namespace Stormancer.Server.Chat
{
    public interface IChatService
    {
        IObservable<ChatMessage> MessagesObservable { get; }

        Task OnMessageReceived(IScenePeerClient client, string message, Action<ChatMessageDto, DestinationType> sender);

        IDisposable OnMessage(Func<ChatMessage, Task> handler);

        Task<List<ChatMessage>> LoadHistory(string channel, DateTime start, DateTime end);

        Task<List<ChatUserInfoDto>> GetConnectedUser();
    }
}

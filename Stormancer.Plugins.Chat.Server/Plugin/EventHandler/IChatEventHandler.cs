using System.Threading.Tasks;

namespace Stormancer.Server.Chat
{
    public interface IChatEventHandler
    {        
        Task OnInit(ChatInitContext ctx);
        Task OnMessageReceived(ChatMsgReceivedContext ctx);
        Task OnShutDown(ChatShutdownContext ctx);
        Task OnDisconnected(ChatDisconnectedContext ctx);
    }  
}
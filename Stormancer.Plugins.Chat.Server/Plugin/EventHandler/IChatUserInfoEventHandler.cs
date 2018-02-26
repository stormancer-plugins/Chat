using Server.Users;
using System.Threading.Tasks;

namespace Stormancer.Server.Chat
{
    public interface IChatUserInfoEventHandler
    {        
        Task GetUserInfo(ChatUserInfoCtx ctx);
    }
    
    public class ChatUserInfoCtx
    {
        public long PeerId { get; set; }
        public User User { get; set; }
        public ChatUserInfoCtx(){}
    }
  
}
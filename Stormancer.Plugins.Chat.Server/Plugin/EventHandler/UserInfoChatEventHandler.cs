using Server.Users;
using System;
using System.Threading.Tasks;

namespace Stormancer.Server.Chat
{
    public class UserInfoChatEventHandler : IChatUserInfoEventHandler
    {
        public IUserSessions _userSession;
        public UserInfoChatEventHandler(IUserSessions userSession)
        {
            _userSession = userSession;
        }

        public async Task GetUserInfo(ChatUserInfoCtx ctx)
        {
            Session s = await _userSession.GetSession(ctx.PeerId);
            if (s == null)
            {
                throw new InvalidOperationException("Error occured when ChatEventHandler try to get a specific user");
            }
            else
            {
                ctx.User = s.User;
            }
        }
    }
}

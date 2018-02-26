using MsgPack.Serialization;

namespace Stormancer.Server.Chat
{
    public class ChatMessageDto
    {
        [MessagePackMember(0)]
        public string Message { get; set; }
        [MessagePackMember(1)]
        public string Metadata { get; set; }
        [MessagePackMember(2)]
        public long TimeStamp { get; set; }
        [MessagePackMember(3)]
        public ChatUserInfoDto UserInfo;
    }
}

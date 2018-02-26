using MsgPack.Serialization;

namespace Stormancer.Server.Chat
{
    [MessagePackEnum(SerializationMethod = EnumSerializationMethod.ByUnderlyingValue)]
    public enum UserStatusDto
    {
        Connected = 1,
        Disconnected = 2,
        Error = 3,        
    }

    public class ChatUserInfoDto
    {
        [MessagePackMember(0)]
        public string UserId { get; set; }
        [MessagePackMember(1)]
        public long PeerId { get; set; }
        [MessagePackMember(2)]        
        public UserStatusDto Status { get; set; }
        [MessagePackMember(3)]
        public string Data { get; set; }
    }
}

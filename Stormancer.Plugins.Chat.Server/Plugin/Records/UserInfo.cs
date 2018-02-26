using Newtonsoft.Json.Linq;

namespace Stormancer.Server.Chat
{
    public class UserInfo
    {
        public string UserId { get; set; }
        public JObject Data { get; set; }
    }
}

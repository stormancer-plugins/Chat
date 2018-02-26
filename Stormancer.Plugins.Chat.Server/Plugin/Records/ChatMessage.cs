using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Stormancer.Server.Chat
{
    public class ChatMessage
    {        
        public string Channel { get; set; }
        public DateTime Date { get; set; }
        public string Message { get; set; }
        public JObject Metadata { get; set; }
        public UserInfo UserInfo { get; set; }
    }
}

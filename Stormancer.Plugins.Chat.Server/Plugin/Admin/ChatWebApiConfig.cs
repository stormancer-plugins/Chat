using Server.Plugins.AdminApi;
using System.Web.Http;

namespace Stormancer.Server.Chat
{
    class ChatWebApiConfig : IAdminWebApiConfig
    {
        public void Configure(HttpConfiguration config)
        {
            // This feature not available yet we need get service on specifique scene.
            // config.Routes.MapHttpRoute("chat", "_chat/{channel}", new {Controller = "ChatAdmin", Action = "ChatAction"});
            config.Routes.MapHttpRoute("hitory", "_seekhistory/{channel}", new { Controller = "ChatAdmin", Action = "HistoryAction" });
        }
    }
}
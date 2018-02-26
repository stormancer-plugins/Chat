using Stormancer.Core;

namespace Stormancer.Server.Chat
{
    public static class ChatExtensions
    {
        public static void AddChat(this ISceneHost scene )
        {
            scene.Metadata[ChatPlugin.METADATA_KEY] = "enabled";         
        }
    }
}

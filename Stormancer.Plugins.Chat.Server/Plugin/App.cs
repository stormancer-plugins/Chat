using Stormancer;

namespace Stormancer.Server.Chat
{
    public class App
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new ChatPlugin());
        }
    }
}

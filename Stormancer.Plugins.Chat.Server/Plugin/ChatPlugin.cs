using Server.Plugins.AdminApi;
using Server.Plugins.Configuration;
using Stormancer;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server;
using System;
using System.Threading;
using System.Threading.Tasks;


// Todo jojo
// Faire un récap des valeurs modifiables du plugin
namespace Stormancer.Server.Chat
{
    class ChatPlugin : IHostPlugin
    {
        internal const string METADATA_KEY = "stormancer.chat";

        private const string _logCategory = "ChatPlugin";
        private IChatRepository _chatLogRepository = null;
        private IConfiguration _config;
        private IHost _host;

        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Task _flushTask = null;
        private int _logCacheDuration = 60;

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.SceneDependenciesRegistration += (IDependencyBuilder builder, ISceneHost scene) =>
              {                 
                  if (scene.Metadata.ContainsKey(METADATA_KEY))
                  {
                      builder.Register<ChatService>().As<IChatService>().InstancePerScene();
                      builder.Register<UserInfoChatEventHandler>().As<IChatUserInfoEventHandler>();
                      builder.Register<ChatAntiFlood>().As<IChatEventHandler>().InstancePerScene();
                      //builder.Register<ChatService.Accessor>();
                      builder.Register<ChatController>().InstancePerRequest();
                  }
              };

            ctx.SceneCreated += (ISceneHost scene) =>
             {
                 if (scene.Metadata.ContainsKey(METADATA_KEY))
                 {
                     scene.AddController<ChatController>();
                     scene.DependencyResolver.Resolve<IChatService>();                     
                     //scene.DependencyResolver.Resolve<ChatService.Accessor>();
                 }
             }; 

            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<ESChatLogRepository>().As<IChatRepository>().SingleInstance();
                builder.Register<ChatWebApiConfig>().As<IAdminWebApiConfig>();
                builder.Register<ChatAdminController>();
            };

            ctx.HostStarted += (IHost host) =>
            {
                _host = host;
                _config = host.DependencyResolver.Resolve<IConfiguration>();
                _config.SettingsChanged += OnSettingsChange;
                OnSettingsChange(_config, _config.Settings);

                _chatLogRepository = host.DependencyResolver.Resolve<IChatRepository>();
                _flushTask = Task.Run(() => FlushSchedule(), _cts.Token);
            };

            ctx.HostShuttingDown += (IHost host) =>
            {
                _cts.Cancel();                
            };
        }

        private void OnSettingsChange(object sender, dynamic settings)
        {
            _logCacheDuration = (int?)settings.chatConfiguration?.maxCacheLogDuration ?? 1;
              
            if(settings.chatConfiguration?.maxCacheLogDuration == null)
            {
                _host.DependencyResolver.Resolve<ILogger>().Log(LogLevel.Warn, _logCategory, $"Failed to find settings in ChatConfiguration -> MaxCacheLogDuration ! Settings is with default value : {_logCacheDuration}", new { maxCacheLogDuration  = _logCacheDuration});
            }
        }

        private async Task FlushSchedule()
        {
            try
            {
                var token = _cts.Token;
                while (!token.IsCancellationRequested)
                {
                    try
                    {            
                        await Task.Delay(_logCacheDuration * 1000, _cts.Token);
                        await _chatLogRepository.Flush();
                    }
                    catch (Exception ex)
                    {
                        _host.DependencyResolver.Resolve<ILogger>().Log(LogLevel.Error, _logCategory, "Failed to push chat log in database", ex);
                    }
                }
                await _chatLogRepository.Flush();
            }
            catch (Exception ex)
            {
                _host.DependencyResolver.Resolve<ILogger>().Log(LogLevel.Error, _logCategory, "Failed to push chat log in database when server shutting down", ex);
            }
        }
    }
}

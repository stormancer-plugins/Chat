using Newtonsoft.Json.Linq;
using Server.Plugins.Configuration;
using Stormancer;
using Stormancer.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Stormancer.Server.Chat
{
    public class ChatAntiFlood : IChatEventHandler, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;
        private const string _logCategory = "ChatAntiFlood";

        private ConcurrentDictionary<string, DateTimeOffset> _jail = new ConcurrentDictionary<string, DateTimeOffset>();
        private ConcurrentDictionary<IScenePeerClient, Task> _inspector = new ConcurrentDictionary<IScenePeerClient, Task>();

        private int _jailDuration;
        private int _windowDuration;
        private int _windowNumberOfMessages;
        private IDisposable _subscription;

        public ChatAntiFlood(IConfiguration config, ILogger logger)
        {
            _logger = logger;

            _config = config;
            _config.SettingsChanged += OnSettingsChange;
            OnSettingsChange(_config, _config.Settings);
        }

        private void OnSettingsChange(object sender, dynamic settings)
        {
            _jailDuration = (int?)settings.chatConfiguration?.jailDuration ?? 3;
            _windowDuration = (int?)settings.chatConfiguration?.windowDuration ?? 5;
            _windowNumberOfMessages = (int?)settings.chatConfiguration?.windowDuration ?? 10;

            if (settings.chatConfiguration?.jailDuration == null)
            {
                _logger.Log(LogLevel.Warn, _logCategory, $"Failed to find jailDuration in configuration. Value is set by default at 3s", new { jailDuration = _jailDuration });
            }

            if (settings.chatConfiguration?._windowDuration == null)
            {
                _logger.Log(LogLevel.Warn, _logCategory, $"Failed to find windowDuration in configuration. Value is set by default at 5s", new { windowDuration = _windowDuration });
            }

            if (settings.chatConfiguration?._windowNumberOfMessages == null)
            {
                _logger.Log(LogLevel.Warn, _logCategory, $"Failed to find windowNumberOfMesages in configuration. Value is set by default at 10s", new { windowNumberOfMessages = _windowNumberOfMessages });
            }
        }

        private void SpamHandler(IObservable<ChatMessage> source)
        {
            _subscription = source
              .GroupBy(message => message.UserInfo.UserId)
              .SelectMany(groupedObservable =>
              {
                  var queue = new Queue<Timestamped<ChatMessage>>();
                  return groupedObservable.Timestamp()
                      .Select(timestampedMessage =>
                      {
                          queue.Enqueue(timestampedMessage);
                          while (timestampedMessage.Timestamp - queue.Peek().Timestamp > TimeSpan.FromSeconds(_windowDuration))
                          {
                              queue.Dequeue();
                          }
                          return new { UserId = groupedObservable.Key, Count = queue.Count };
                      });
              })
              .Where(messageLimit => messageLimit.Count > _windowNumberOfMessages)
              .Subscribe(culprit =>
              {
                    Ban(culprit.UserId, TimeSpan.FromSeconds(_jailDuration));               
              });
        }

        private void Ban(string UserId, TimeSpan duration)
        {
            var temp = DateTimeOffset.UtcNow;
            DateTimeOffset releaseTime = DateTimeOffset.UtcNow + duration;
            var newReleaseTime = _jail.AddOrUpdate(UserId, releaseTime, (userId, previousReleaseTime) => releaseTime < previousReleaseTime ? previousReleaseTime: releaseTime);                      
        }

        private Task<bool> UserIsInJail(string userID)
        {
            DateTimeOffset releaseTime;
            bool inJail = false;
            if(_jail.TryGetValue(userID, out releaseTime))
            {
                if(releaseTime >= DateTimeOffset.UtcNow)
                {
                    inJail = true;
                }
                else
                {
                    _jail.TryRemove(userID, out releaseTime);
                }
            }
            return Task.FromResult(inJail);          
        }

        public async Task OnInit(ChatInitContext ctx)
        {
            SpamHandler(ctx.Messages);
            await Task.CompletedTask;
        }

        public async Task OnMessageReceived(ChatMsgReceivedContext ctx)
        {
            bool inJail = await UserIsInJail(ctx.Message.UserInfo.UserId);
            if(inJail)
            {
                ctx.Destination = DestinationType.Self;
                ctx.Message.Metadata["flood"] = true;
                ctx.MessageDto.Metadata  = ctx.Message.Metadata.ToString();                
            }
            await Task.CompletedTask;
        }

        public Task OnDisconnected(ChatDisconnectedContext ctx)
        {
            DateTimeOffset _;
            _jail.TryRemove(ctx.UserId, out _);
            return Task.CompletedTask;
        }

        public async Task OnShutDown(ChatShutdownContext ctx)
        {
            _subscription?.Dispose();
            _subscription = null;

            await Task.CompletedTask;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    _subscription?.Dispose();
                    _subscription = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ChatAntiFlood() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
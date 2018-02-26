using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Stormancer;
using Stormancer.Core;
using Stormancer.Server.Components;
using Stormancer.Diagnostics;
using Server.Plugins.Configuration;
using System.Reactive.Linq;
using Newtonsoft.Json.Linq;
using System.Reactive.Subjects;

namespace Stormancer.Server.Chat
{
    public class ChatService : IChatService
    {
        //Accessor
        //public class Accessor
        //{
        //    public Accessor(IChatService service, Func<IEnumerable<IChatEventHandler>> handlers)
        //    {
        //        ((ChatService)service).EventHandlers = handlers;
        //        Service = service;
        //    }
        //    public IChatService Service { get; }
        //}

        //public IEnumerable<IChatEventHandler> EventHandlers
        //{
        //    set
        //    {
        //        _eventHandlers = value;
        //    }
        //}

        // Utils
        private const string _logCategory = "ChatService";

        //Needed dependency
        private readonly ISceneHost _scene;
        private IEnumerable<IChatUserInfoEventHandler> _eventUserHandlers;
        private readonly IEnumerable<IChatEventHandler> _chatEventHandler;
        private readonly IChatRepository _chatRepository;
        private readonly IConfiguration _config;
        private readonly IEnvironment _env;
        private readonly ILogger _log;

        // Internal uses
        private ConcurrentDictionary<IScenePeerClient, ChatUserInfoDto> _connectedUsers = new ConcurrentDictionary<IScenePeerClient, ChatUserInfoDto>();
        private Subject<ChatMessage> _messagesSource = new Subject<ChatMessage>();

        // Config
        private bool _loadHistory;

        public IObservable<ChatMessage> MessagesObservable
        {
            get
            {
                return _messagesSource;
            }
        }

        public ChatService(ISceneHost scene, 
            ILogger log, 
            IEnvironment env, 
            IChatRepository chatRepository, 
            IConfiguration configuration, 
            IEnumerable<IChatUserInfoEventHandler> chatUserEventHandlers,
            IEnumerable<IChatEventHandler> chatEventHandler
        )
        {
            //Handler
            _eventUserHandlers = chatUserEventHandlers;
            _chatEventHandler = chatEventHandler;

            _scene = scene;
            _log = log;
            _env = env;

            _config = configuration;
            _config.SettingsChanged += OnSettingsChange;
            OnSettingsChange(_config, _config.Settings);

            _chatRepository = chatRepository;

            _scene.Connected.Add(OnConnected);
            _scene.Disconnected.Add(OnDisconnected);
            _scene.Starting.Add(OnChatStarting);
            _scene.Shuttingdown.Add(OnChatShuttingdown);
        }

        private async Task OnChatStarting(dynamic dyn)
        {
            //Init Antiflood
            ChatInitContext chatInitCtx = new ChatInitContext(_messagesSource);
            await _chatEventHandler?.RunEventHandler(eh => eh.OnInit(chatInitCtx), ex =>
            {
                _log.Log(LogLevel.Warn, _logCategory, $"Failed when [chatService] try to load chatEventHandler", new {});
            });
        }

        private async Task OnChatShuttingdown(ShutdownArgs args)
        {
            ChatShutdownContext chatShutdown = new ChatShutdownContext(MessagesObservable);
            await _chatEventHandler?.RunEventHandler(eh =>
            {
                return eh.OnShutDown(chatShutdown);
            }, ex =>
            {
                _log.Log(LogLevel.Error, _logCategory, "An error occured when the chat service try to dispose observable.", ex);
                throw new ClientException($"An error occured when the chat service try to dispose observable.");
            });
        }

        private void OnSettingsChange(object sender, dynamic settings)
        {
            if((bool?)settings.chatConfiguration?.loadHistory == null)
            {
                _loadHistory = false;
                _log.Log(LogLevel.Warn, _logCategory, $"Failed to find settings in ChatConfiguration -> LoadHistory ! Settings is with default value : { _loadHistory }", new { loadHistory = _loadHistory });
            }
            else
            {
                _loadHistory = settings.chatConfiguration?.loadHistory;
            }
        }

        #region History        
        public async Task<List<ChatMessage>> LoadHistory(string channel, DateTime start, DateTime end)
        {
            if (_loadHistory)
            {
                return await _chatRepository.SeekHistoryMessage(channel, start, end);
            }
            throw new ClientException("Cache history is disable");
        }
        #endregion

        // Todo jojo : vérifier avec JN ou JM comment faire pour la gestion des ban et des erreurs lors du get info
        // Pour ne pas ajouter un utilisatuer alors qu'il est déjà connecté
        // Todo jojo : ajouter une métadata pour mettre la/les raisons de l'erreur
        private async Task OnConnected(IScenePeerClient clientPeer)
        {            

            ChatUserInfoDto connectingUserInfo = new ChatUserInfoDto { PeerId = clientPeer.Id };

            ChatUserInfoCtx chatCtx = new ChatUserInfoCtx{ PeerId = clientPeer.Id };          

            await _eventUserHandlers?.RunEventHandler(eh => eh.GetUserInfo(chatCtx), ex => 
            {
                _log.Log(LogLevel.Error, _logCategory, "An error occured while running ChatService.GetUserInfo event handlers", ex);               
                connectingUserInfo = new ChatUserInfoDto { PeerId = Convert.ToInt64(clientPeer.Id.ToString()), Status = UserStatusDto.Error };

                clientPeer.Send("statuschange", (s) => {
                    clientPeer.Serializer().Serialize((int)UserStatusDto.Error, s);
                    clientPeer.Serializer().Serialize(connectingUserInfo, s);
                },
                    PacketPriority.MEDIUM_PRIORITY,
                    PacketReliability.RELIABLE
                );                
            });  
           
            //Send list of connected user
            if (!_connectedUsers.Any(x => x.Value.PeerId == connectingUserInfo.PeerId))
            {
                connectingUserInfo.UserId = chatCtx.User.Id;
                connectingUserInfo.Status = UserStatusDto.Connected;
                connectingUserInfo.Data = chatCtx.User.UserData.ToString();                   

                _connectedUsers.TryAdd(clientPeer, connectingUserInfo);
                List<ChatUserInfoDto> usersInChat = _connectedUsers.Values.ToList<ChatUserInfoDto>();

                _scene.Broadcast("statuschange", (s) => {
                    clientPeer.Serializer().Serialize((int)UserStatusDto.Connected, s);
                    clientPeer.Serializer().Serialize(usersInChat, s);
                });               
            }       
        }

        private async Task OnDisconnected(DisconnectedArgs args)
        {
            ChatUserInfoDto disconnectingInfo = null;
            _connectedUsers.TryRemove(args.Peer, out disconnectingInfo);

            if (disconnectingInfo == null)
            {
                throw new InvalidOperationException("Client doesn't connected.");
            }

            disconnectingInfo.Status = UserStatusDto.Disconnected;

            _scene.Broadcast("statuschange", (s) => {
                args.Peer.Serializer().Serialize((int)UserStatusDto.Disconnected, s);
                args.Peer.Serializer().Serialize(disconnectingInfo, s);
            });

            ChatDisconnectedContext chatDisconnected = new ChatDisconnectedContext(disconnectingInfo.UserId);
            await _chatEventHandler?.RunEventHandler(eh =>
            {
                return eh.OnDisconnected(chatDisconnected);
            }, ex =>
            {
                _log.Log(LogLevel.Error, _logCategory, "An error occured when the chat service try to dispose observable.", ex);
                throw new ClientException($"An error occured when the chat service try to dispose observable.");
            });            
        }

        public async Task OnMessageReceived(IScenePeerClient client, string text, Action<ChatMessageDto,DestinationType> sender)
        {
            // Check if user is connected   
            ChatUserInfoDto userInfo = null;
            _connectedUsers.TryGetValue(client, out userInfo);
            if (userInfo == null)
            {
                throw new ClientException($"User not found {client.Id} .");
            }

            // Store message in log
            ChatMessage messageLog = new ChatMessage
            {                
                Channel = client.SceneId,
                Date = DateTime.UtcNow,
                Message = text,
                Metadata = new JObject(),
                UserInfo = new UserInfo
                {
                    UserId = userInfo.UserId,
                    Data = JObject.Parse(userInfo.Data)
                }
            };

            ChatMessageDto messageDto = new ChatMessageDto
            {
                Message = text,
                TimeStamp = _env.Clock,
                UserInfo = userInfo,
            };
            
            ChatMsgReceivedContext receiveCtx = new ChatMsgReceivedContext(messageLog, messageDto);
            receiveCtx.Destination = DestinationType.All;

            await _chatEventHandler?.RunEventHandler(eh =>
            {
                return eh.OnMessageReceived(receiveCtx);
            }, ex =>
            {
                _log.Log(LogLevel.Error, _logCategory, "An error occured when the chat service check if the user is in jail.", ex);
                throw new ClientException($"An error occured when the chat service check if the user is in jail.");
            });
         
            _chatRepository.AddMessageLog(receiveCtx.Message);
            _messagesSource.OnNext(receiveCtx.Message);
            sender(receiveCtx.MessageDto, receiveCtx.Destination);
        }

        public IDisposable OnMessage(Func<ChatMessage, Task> handler)
        {
            return MessagesObservable.Subscribe((messageChat) => { handler(messageChat); });
        }

        public Task<List<ChatUserInfoDto>> GetConnectedUser()
        {
            return Task.FromResult(_connectedUsers.Values.ToList<ChatUserInfoDto>());
        }
    }
}

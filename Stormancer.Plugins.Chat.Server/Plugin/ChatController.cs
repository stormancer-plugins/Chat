using System.Threading.Tasks;
using Stormancer;
using Stormancer.Core;
using Server.Plugins.API;
using Stormancer.Diagnostics;
using System;
using System.Collections.Generic;
using Stormancer.Plugins;
using Server.Helpers;

namespace Stormancer.Server.Chat
{
    public class ChatController : ControllerBase
    {

        private const string _logCategory = "ChatController";
        private readonly IChatService _chat;
        private readonly ILogger _logger;
        private readonly ISceneHost _scene;

        public ChatController(ISceneHost scene, IChatService chat, ILogger log)
        {
            _scene = scene;
            _chat = chat;
            _logger = log;
        }

        public async Task Message(RequestContext<IScenePeerClient> ctx)
        {
            try
            {
                await _chat.OnMessageReceived(ctx.RemotePeer, ctx.ReadObject<string>(), (messageDto, destination) =>
                {
                    if (messageDto == null)
                    {
                        throw new InvalidOperationException("Message recieved internal error");
                    }

                    if (destination.HasFlag(DestinationType.Others))
                    {
                        _scene.Broadcast("receivemessage", messageDto);
                    }

                    if (destination.HasFlag(DestinationType.Self))
                    {
                        ctx.SendValue(messageDto);
                    }

                });
            }            
            catch(Exception ex)
            {
                _logger.Log(LogLevel.Error, _logCategory, "Error occured when server received message", ex);
                throw new ClientException(ex.Message);
            }          
        }

        public async Task LoadHistory(RequestContext<IScenePeerClient> ctx)
        {
            List<ChatMessageDto> result = new List<ChatMessageDto>();
            try
            {
                long startTimestamp = ctx.ReadObject<long>();
                long endTimestamp = ctx.ReadObject<long>();

                DateTime start = TimestampHelper.UnixTimeStampSecondToDateTime(startTimestamp);
                DateTime end = TimestampHelper.UnixTimeStampSecondToDateTime(endTimestamp);

                string channelName = ctx.RemotePeer.SceneId;
                var messagesData = await _chat.LoadHistory(channelName, start, end);

                foreach(ChatMessage msg in messagesData)
                {
                    var message = new ChatMessageDto {
                        Message = msg.Message,
                        TimeStamp = TimestampHelper.DateTimeToUnixTimeStamp(msg.Date),
                        UserInfo = new ChatUserInfoDto
                        {
                            UserId = msg.UserInfo.UserId,
                            Data = msg.UserInfo.Data.ToString(),
                        }
                    };
                    result.Add(message);
                }
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, _logCategory, "Error occured when server try to load history", ex);
                throw new ClientException(ex.Message);
            }

            ctx.SendValue(result);
        }
    }
}

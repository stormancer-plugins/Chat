using Stormancer.Diagnostics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Server.Helpers;

namespace Stormancer.Server.Chat
{
    public class ChatAdminController : ApiController
    {
        private const string _logCategory = "ChatAdminController";
        private readonly IChatRepository _chatRepository;
        private readonly ILogger _logger;

        // Todo jojo: at this time i can't get a service from a scene. I will get messages from data base
        public ChatAdminController(IChatRepository chatRepository, ILogger log)
        {
            _chatRepository = chatRepository;
            _logger = log;
        }

        [ActionName("HistoryAction")]
        [HttpGet]
        public async Task<List<ChatMessageDto>> SeekHistory(string channel, string dateStart, string dateEnd)
        {
            List<ChatMessage> messagesData = new List<ChatMessage>();
            List<ChatMessageDto> result = new List<ChatMessageDto>();
            try
            {
                DateTime start = DateTime.ParseExact(dateStart, "ddMMyyyyHH:mm:ss", CultureInfo.InvariantCulture);
                DateTime end = DateTime.ParseExact(dateEnd, "ddMMyyyyHH:mm:ss", CultureInfo.InvariantCulture);
                messagesData = await _chatRepository.SeekHistoryMessage(channel, start, end);
            }
            catch (ArgumentNullException argumentEx)
            {
                throw HttpHelper.HttpError(System.Net.HttpStatusCode.BadRequest, "Some argument are null");
            }
            catch(FormatException formatEx)
            {
                throw HttpHelper.HttpError(System.Net.HttpStatusCode.BadRequest, "Date format not supported");
            }
            catch(Exception ex)
            {
                _logger.Log(LogLevel.Error, _logCategory, "An error occured when seek history in chat repository", ex.Message);
                throw HttpHelper.HttpError(System.Net.HttpStatusCode.InternalServerError, "Server Internal error(s)");
            }

            if(result.Count == 0)
            {
                throw HttpHelper.HttpError(System.Net.HttpStatusCode.NoContent, $"No data found in request date range DateStart={dateStart}, DateEnd={dateEnd}");
            }
            else
            {
                foreach (ChatMessage msg in messagesData)
                {
                    var message = new ChatMessageDto
                    {
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
                return result;
            }
        }

        // This feature not available yet we need get service on specifique scene.
        // [ActionName("ChatAction")]
        // [HttpGet]
        // public async Task<List<ChatUserInfoDto>> GetUsers(string channel, HttpRequestMessage message)
        // {
        //     EnsureRequestValid(message);
        //     try
        //     {
        //         return await chatRepository.GetConnectedUser();
        //     }
        //     catch (ArgumentNullException ex)
        //     {
        //         throw HttpError(System.Net.HttpStatusCode.BadRequest, ex.Message);   
        //     }
        //}

        /// <summary>
        /// Check if used verb is allowed
        /// </summary>
        /// <param name="request">Initial request</param>
        private void EnsureRequestValid(HttpRequestMessage request)
        {
            if(request.Method == HttpMethod.Delete)
            {
                throw HttpHelper.HttpError(System.Net.HttpStatusCode.Forbidden, "DELETE requests are forbidden");
            }
            if(request.Method == HttpMethod.Put)
            {
                throw HttpHelper.HttpError(System.Net.HttpStatusCode.Forbidden, "PUT requests are forbidden");
            }           
        }
    }
}

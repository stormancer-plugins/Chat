using Microsoft.CSharp.RuntimeBinder;
using Server.Database;
using Server.Plugins.Configuration;
using Stormancer;
using Stormancer.Diagnostics;
using Stormancer.Server.Components;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stormancer.Server.Chat
{
    public class ESChatLogRepository : IChatRepository
    {
        private const string LOG_CATEGORY = "ESChatLogRepository";
        private const string TABLE_NAME = "chatlog";
        private const string DEFAULT_TYPE = "ChatMessage";
        //_NbrMessagesToKeep messages are kept in memory. Older ones are deleted when new messages kicks in.
        private ConcurrentQueue<ChatMessage> _messagesCache = new ConcurrentQueue<ChatMessage>();
        private readonly IConfiguration _conf;
        private readonly IEnvironment _env;
        private readonly IESClientFactory _esClient;
        private readonly ILogger _log;

        private int _batchSize;
        private int _maxChatLogSize;

        public ESChatLogRepository(IEnvironment env, IConfiguration conf, IESClientFactory esClient, ILogger log)
        {
            _env = env;
            _log = log;
            _esClient = esClient;

            _conf = conf;
            _conf.SettingsChanged += OnSettingsChange;
            OnSettingsChange(_conf, _conf.Settings);
        }

        private async void OnSettingsChange(object sender, dynamic settings)
        {
            _batchSize = (int?)settings.chatConfiguration?.batchSize ?? 1000;
            _maxChatLogSize = (int?)settings.chatConfiguration?.maxChatLogSize ?? 1000;

            if (settings.chatConfiguration?.batchSize == null)
            {
                _log.Log(LogLevel.Warn, LOG_CATEGORY, $"Failed to find settings in chatConfiguration -> batchSize ! Settings is with default value : { _batchSize }", new { batchSize =_batchSize });
            }

            if (settings.chatConfiguration?.maxChatLogSize == null)
            {
                _log.Log(LogLevel.Warn, LOG_CATEGORY, $"Failed to find settings in chatConfiguration -> maxChatLogSize ! Settings is with default value : { _maxChatLogSize }", new { maxChatLogSize = _maxChatLogSize });
            }
        }

        #region ElasticSearch
        private async Task<Nest.IElasticClient> CreateESClient<T>(string parameters)
        {
            var result = await _esClient.CreateClient<T>(TABLE_NAME, parameters);
            return result;
        }

        // Rajouter les checks
        private async Task<List<ChatMessage>> ESQuery(string channel, DateTime start, DateTime end)
        {
            // create list of result message log
            List<ChatMessage> messagesLog = new List<ChatMessage>();
            List<string> indexes = MultipleIndiceParameter(GetWeeks(start, end), new string[] { DEFAULT_TYPE });

            var esClient = await CreateESClient<ChatMessage>(GetWeek(DateTime.UtcNow).ToString());
    
            var scanResults = await esClient.SearchAsync<ChatMessage>(s => s
                .Query(q => q
                    .Bool(b => b
                        .Must(m => m
                        .Term("channel.keyword", channel)
                        )
                    )
                )
                .Index(Nest.Indices.Index(indexes))
                .Scroll("1s")
                .Size(50)
                .IgnoreUnavailable()
            );

            if (!scanResults.IsValid)
            {
                _log.Log(LogLevel.Error, LOG_CATEGORY, $"An error occured when the server try to request database", scanResults.OriginalException);
                throw new ClientException("Database not found");
            }

            messagesLog.AddRange(scanResults.Documents);
            while (scanResults.Documents.Any())
            {
                scanResults = await esClient.ScrollAsync<ChatMessage>("1s", scanResults.ScrollId);
                messagesLog.AddRange(scanResults.Documents);
            }

            await esClient.ClearScrollAsync(d => d.ScrollId(scanResults.ScrollId), default(System.Threading.CancellationToken));
                            
            return messagesLog.Where((x) =>
            {
                return x.Channel == channel && x.Date >= start && x.Date <= end;
            }).ToList<ChatMessage>();
        }

        private Task<List<ChatMessage>> Memory(string channel, DateTime start, DateTime end)
        {
            List<ChatMessage> msgLog = _messagesCache.ToList();
            return Task.FromResult<List<ChatMessage>>(msgLog.Where((x) =>
            {
                return x.Channel == channel && x.Date >= start && x.Date <= end;
            }).ToList<ChatMessage>());
        }
        #endregion

        #region DataHandling
        // Get Multiple indice
        private List<string> MultipleIndiceParameter(IEnumerable<int> weeks, string[] types)
        {
            List<String> indexes = new List<string>();
            foreach (string type in types)
            {
                foreach (int week in weeks)
                {
                    indexes.Add(_esClient.GetIndex(type, TABLE_NAME, week));
                }
            }
            return indexes;
        }

        // Get single indice
        private string SingleIndiceParameter(string type, long week)
        {
            return _esClient.GetIndex(type, TABLE_NAME, week.ToString());
        }

        private IEnumerable<int> GetWeeks(DateTime start, DateTime end)
        {
            var endWeek = (int)GetWeek(end) - (int)GetWeek(start);
            return Enumerable.Range((int)GetWeek(start), endWeek == 0 ? 1 : endWeek);
        }

        /// <summary>
        /// Get week number.
        /// </summary>
        /// <param name="date"></param>
        /// <returns>return weak number</returns>
        private long GetWeek(DateTime date)
        {           
            return date.Ticks / (TimeSpan.TicksPerDay * 7);
        }
        #endregion

        #region RepositoryAction

        public void AddMessageLog(ChatMessage messageLog)
        {
            _messagesCache.Enqueue(messageLog);
            if(_maxChatLogSize <= _messagesCache.Count)
            {
                Flush();
            }
        }

        public async Task<List<ChatMessage>> SeekHistoryMessage(string channel, DateTime start, DateTime end)
        {
            ChatMessage firstQueueMessage;
            _messagesCache.TryPeek(out firstQueueMessage);

            if (firstQueueMessage == null)
            {
                firstQueueMessage = new ChatMessage { Channel = channel, Date = DateTime.UtcNow};
            }

            if (end < start)
            {
                DateTime temp = start;
                start = end;
                end = temp;
            }

            //Base
            List<ChatMessage> messagesInBase = new List<ChatMessage>();
            if (firstQueueMessage.Date > start)
            {
                messagesInBase = await ESQuery(channel, start, new DateTime(Math.Min(end.Ticks, firstQueueMessage.Date.Ticks)));
            }

            //memory
            List<ChatMessage> messagesInMem = new List<ChatMessage>();
            if (firstQueueMessage.Date <= end)
            {
                // check all in memory
                messagesInMem = await Memory(channel, new DateTime(Math.Max(start.Ticks, firstQueueMessage.Date.Ticks)), end);
            }

            return messagesInBase.Concat(messagesInMem).OrderBy(order => order.Date).ToList();
        }

        private Task _flushTask = Task.CompletedTask;
        private object _flushSyncRoot = new object();
        public Task Flush()
        {
            if (_flushTask.IsCompleted)
            {
                lock (_flushSyncRoot)
                {
                    if (_flushTask.IsCompleted)
                    {
                        _flushTask = FlushImpl();
                    }
                }
            }
            return _flushTask;
        }

        private  async Task FlushImpl()
        {
            int messageToDequeue = _messagesCache.Count;
            int iteration = (int)Math.Truncate((decimal)(messageToDequeue / _batchSize));
            float scrap = ((float)messageToDequeue / _batchSize - iteration) * _batchSize;

            List<List<ChatMessage>> batches = new List<List<ChatMessage>>();
            for (int i = 0; i < iteration; i++)
            {
                Console.WriteLine("Batch = " + i);
                List<ChatMessage> batch = new List<ChatMessage>();
                for (int j = 0; j < _batchSize; j++)
                {
                    ChatMessage item;
                    _messagesCache.TryDequeue(out item);
                    batch.Add(item);
                }
                batches.Add(batch);
            }

            List<ChatMessage> batchScrap = new List<ChatMessage>();
            for (int i = 0; i < scrap; i++)
            {
                ChatMessage item;
                _messagesCache.TryDequeue(out item);
                batchScrap.Add(item);
            }
            batches.Add(batchScrap);

            // Make elastic shearch resquest           
            long currentWeek = GetWeek(DateTime.UtcNow);          

            // Todo database Put a fix to get indexes by data
            var esClient = await CreateESClient<ChatMessage>(currentWeek.ToString());
            foreach (var messagesLog in batches)
            {
                await esClient.BulkAsync(d => d.IndexMany(messagesLog));
            }
        }
        #endregion
    }
}

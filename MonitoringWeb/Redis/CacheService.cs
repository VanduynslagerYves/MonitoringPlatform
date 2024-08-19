using MonitoringWeb.Helpers;
using MonitoringWeb.Model;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Diagnostics;
using IServer = StackExchange.Redis.IServer;

namespace MonitoringWeb.Redis
{
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key);
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
        Task AddAsync<T>(string hostName, T systemInfo, TimeSpan? expiry = null) where T : SystemInfoRecord;
        Task<List<T>> GetPagedAsync<T>(int pageSize = 0) where T : SystemInfoRecord;
        int GetTotalRecordCount();
    }

    public class CacheService : ICacheService
    {
        private readonly IDatabase _db;
        private readonly IServer _server;

        public CacheService(RedisConnection redisConnection)
        {
            var redis = redisConnection.Connection;
            _db = redis.GetDatabase(); // Get the default database
            _server = redis.GetServer(redis.GetEndPoints().First()); // Get the server for key enumeration
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            var jsonValue = JsonConvert.SerializeObject(value);
            await _db.StringSetAsync(key, jsonValue, expiry);
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            var value = await _db.StringGetAsync(key);
            if (!value.IsNullOrEmpty)
            {
                return JsonConvert.DeserializeObject<T>(value!);
            }
            return default;
        }

        public async Task AddAsync<T>(string hostName, T systemInfo, TimeSpan? expiry = null) where T : SystemInfoRecord
        {
            try
            {
                // Serialize and store the record
                var jsonRecord = JsonConvert.SerializeObject(systemInfo);
                await _db.StringSetAsync(hostName, jsonRecord, expiry);
            }
            catch (RedisTimeoutException)
            {
                Debug.WriteLine($"Timeout while retreiving cached data. {0}", DebugHelper.GetCurrentClassMethodAndLine());
            }
            catch (RedisConnectionException)
            {
                Debug.WriteLine($"Could not establish connection to Redis. {0}", DebugHelper.GetCurrentClassMethodAndLine());
            }
        }

        public async Task<List<T>> GetPagedAsync<T>(int pageSize = 0) where T : SystemInfoRecord
        {
            var records = new List<T>();

            try
            {
                var allKeys = pageSize == 0 ?
                    _server.Keys(pattern: "*").ToList() : // Get all keys
                    _server.Keys(pattern: "*").Take(pageSize).ToList();

                foreach (var key in allKeys)
                {
                    var jsonRecord = await _db.StringGetAsync(key);
                    if (!jsonRecord.IsNullOrEmpty)
                    {
                        var record = JsonConvert.DeserializeObject<T>(jsonRecord!);
                        records.Add(record!);
                    }
                }
            }
            catch (RedisTimeoutException)
            {
                Debug.WriteLine($"Timeout while retreiving cached data. {0}", DebugHelper.GetCurrentClassMethodAndLine());
            }
            catch (RedisConnectionException)
            {
                Debug.WriteLine($"Could not establish connection to Redis. {0}", DebugHelper.GetCurrentClassMethodAndLine());
            }

            return records.OrderBy(x => x.HostName).ToList();
        }

        public int GetTotalRecordCount()
        {
            var allKeys = _server.Keys(pattern: "*").ToList();

            return allKeys.Count;
        }
    }
}

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
        Task<Dictionary<string, T>> GetAllAsync<T>() where T : SystemInfoRecord;
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
                var existingRecordJson = _db.StringGet(hostName);
                if (!existingRecordJson.IsNullOrEmpty)
                {
                    var existingRecord = JsonConvert.DeserializeObject<T>(existingRecordJson!);
                    if (systemInfo.TimeStamp <= existingRecord!.TimeStamp)
                    {
                        return; // Do not update if the existing record has a more recent timestamp, so only the last record for the hostname will be saved to cache.
                    }
                }

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

        // Retrieve all records
        public async Task<Dictionary<string, T>> GetAllAsync<T>() where T : SystemInfoRecord
        {
            var records = new Dictionary<string, T>();

            try
            {
                var allKeys = _server.Keys(pattern: "*").ToList(); // Get all keys

                foreach (var key in allKeys)
                {
                    var jsonRecord = await _db.StringGetAsync(key);
                    if (!jsonRecord.IsNullOrEmpty)
                    {
                        var record = JsonConvert.DeserializeObject<T>(jsonRecord!);
                        records[key!] = record!;
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

            return records;
        }
    }
}

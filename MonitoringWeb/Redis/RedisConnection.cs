using StackExchange.Redis;
using System.Diagnostics;

namespace MonitoringWeb.Redis
{
    public class RedisConnection
    {
        private readonly ConnectionMultiplexer _redis;
        public RedisConnection(string connectionString = "localhost:6379") //TODO: read from appsettings
        {
            while (_redis == null || !_redis.IsConnected)
            {
                try
                {
                    _redis = ConnectionMultiplexer.Connect(connectionString);
                }
                catch (RedisConnectionException)
                {
                    Debug.WriteLine("Could not establish connection to Redis");
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            }
        }

        public ConnectionMultiplexer Connection
        {
            get
            {
                return _redis;
            }
        }
    }
}

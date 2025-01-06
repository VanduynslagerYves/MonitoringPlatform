using StackExchange.Redis;
using System.Diagnostics;

namespace MonitoringWeb.Redis;

public class RedisConnection
{
    public RedisConnection(string connectionString = "localhost:6379") //TODO: read from appsettings
    {
        while (Connection == null || !Connection.IsConnected)
        {
            try
            {
                Connection = ConnectionMultiplexer.Connect(connectionString);
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

    public ConnectionMultiplexer Connection { get; }
}

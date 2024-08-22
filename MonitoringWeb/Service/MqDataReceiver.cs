using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using MonitoringWeb.Config;
using MonitoringWeb.Helpers;
using MonitoringWeb.Hubs;
using MonitoringWeb.Model;
using MonitoringWeb.Redis;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using SystemTimer = System.Timers.Timer;

namespace MonitoringWeb.Service
{
    /// <summary>
    /// Background hosted service that will process the messagequeue containing data from the clients
    /// </summary>
    public class MqDataReceiver : BackgroundService //TODO: place in separate background-service
    {
        private IConnection? _connection;
        private IModel? _receiveChannel;

        private readonly IServiceProvider _serviceProvider;
        private readonly ICacheService _cacheService;
        private readonly IHubContext<DataHub> _hubContext;
        private readonly IConnectionFactory _factory;

        //TODO: Read from appsettings.json, increase for higher client count, decrease for lower client count, sync with prefetchCount
        //TODO: mechanism to decrease or increase this value at runtime based on the number of reporting clients.
        //example: 1 client and _processedTreshold of 10 will be too slow if it reports every 5 seconds
        private const int _processedTreshold = 20;
        private int _processedCount = 0;

        public MqDataReceiver(IServiceProvider serviceProvider, IOptions<RabbitMQConfig> mqConfig, ICacheService cacheService, IHubContext<DataHub> hubContext)
        {
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;

            _factory = new ConnectionFactory
            {
                HostName = mqConfig.Value.Uri,
                Port = mqConfig.Value.Port,
                UserName = mqConfig.Value.UserName,
                Password = mqConfig.Value.Password,
            };

            _cacheService = cacheService;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (_receiveChannel == null)
            {
                try
                {
                    _connection = _factory.CreateConnection();
                    _receiveChannel = _connection.CreateModel();

                    _receiveChannel.QueueDeclare(queue: "monitor_service_queue",
                        durable: false, exclusive: false, autoDelete: false, arguments: null);

                    _receiveChannel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);
                }
                catch (BrokerUnreachableException)
                {
                    Debug.WriteLine($"RabbitMQ is unreachable: {DebugHelper.GetCurrentClassMethodAndLine()}");
                }
            }

            //int processedCount = 0;
            //var timer = new SystemTimer();
            //timer.Interval = 5000; //ms
            //timer.AutoReset = true;
            //timer.Start();

            var systemInfoConsumer = new EventingBasicConsumer(_receiveChannel);
            systemInfoConsumer.Received += OnItemReceived;

            _receiveChannel.BasicConsume(queue: "monitor_service_queue", autoAck: false, consumer: systemInfoConsumer);

            return Task.CompletedTask;
        }

        private async void OnItemReceived(object? sender, BasicDeliverEventArgs ea)
        {
            try
            {
                var body = ea.Body.ToArray();
                var jsonMessage = Encoding.UTF8.GetString(body);

                var record = JsonConvert.DeserializeObject<SystemInfoRecord>(jsonMessage);
                if (record != null)
                {
                    record.Id = Guid.NewGuid();

                    // Save record to cache with 30s expiration timespan
                    await _cacheService.AddAsync<SystemInfoRecord>(hostName: record.HostName, systemInfo: record, expiry: TimeSpan.FromMinutes(1));

                    //await _hubContext.Clients.All.SendAsync($"NotifyDataUpdate:all");

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
                        // Save record to db
                        await dataService.AddAsync(record);
                    }

                    //timer.Elapsed += OnTimeElapsed;

                    _processedCount++;
                    if (_processedCount >= _processedTreshold) //We will only notify of new data when there are 10 or more items consumed and saved to the cache and database.
                    {
                        Debug.WriteLine($"{DateTime.Now}: {_processedTreshold} or more items consumed, calling NotifyDataUpdate:all");
                        _processedCount = 0;

                        //notify clients of model change, the client will fetch the new data
                        await _hubContext.Clients.All.SendAsync($"NotifyDataUpdate:all");
                    }
                }

                _receiveChannel!.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (AlreadyClosedException e)
            {
                Debug.WriteLine(e.Message);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        //private async void OnTimeElapsed(object? sender, ElapsedEventArgs e)
        //{
        //    Debug.WriteLine($"{DateTime.Now}: {_processedTreshold} or more items consumed, calling NotifyDataUpdate:all");
        //    //processedCount = 0;

        //    //notify clients of model change, the client will fetch the new data
        //    await _hubContext.Clients.All.SendAsync($"NotifyDataUpdate:all");
        //}
    }
}

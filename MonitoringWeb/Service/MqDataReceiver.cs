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

namespace MonitoringWeb.Service
{
    /// <summary>
    /// Background hosted service that will process the messagequeue containing data from the clients
    /// </summary>
    public class MqDataReceiver : BackgroundService
    {
        private IConnection? _connection;
        private IModel? _receiveChannel;
        private IServiceProvider _serviceProvider;
        private readonly ICacheService _cacheService;

        private readonly IHubContext<DataHub> _hubContext;

        private readonly IConnectionFactory _factory;

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

            var createConsumer = new EventingBasicConsumer(_receiveChannel);
            createConsumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var jsonMessage = Encoding.UTF8.GetString(body);

                var record = JsonConvert.DeserializeObject<SystemInfoRecord>(jsonMessage);
                if (record != null)
                {
                    record.Id = Guid.NewGuid();

                    // Save record to cache with 30s expiration timespan
                    await _cacheService.AddAsync<SystemInfoRecord>(hostName: record.HostName, systemInfo: record, expiry: TimeSpan.FromMinutes(1));
                    //notify clients of model change, the client will fetch the new data
                    await _hubContext.Clients.All.SendAsync($"NotifyDataUpdate:all");

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
                        // Save record to db
                        await dataService.AddAsync(record);
                    }
                }

                try
                {
                    _receiveChannel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch(AlreadyClosedException e)
                {
                    Debug.WriteLine(e.Message);
                }
            };

            _receiveChannel.BasicConsume(queue: "monitor_service_queue", autoAck: false, consumer: createConsumer);

            return Task.CompletedTask;
        }
    }
}

using Microsoft.Extensions.Options;
using MonitoringWeb.Config;
using MonitoringWeb.Helpers;
using MonitoringWeb.Model;
using MonitoringWeb.Redis;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

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

        private readonly IConnectionFactory _factory;

        public MqDataReceiver(IServiceProvider serviceProvider, IOptions<RabbitMQConfig> mqConfig, ICacheService cacheService)
        {
            _serviceProvider = serviceProvider;

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

                var record = JsonSerializer.Deserialize<SystemInfoRecord>(jsonMessage);
                if (record != null)
                {
                    record.Id = Guid.NewGuid();

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();

                        // Save record to cache with 30s expiration timespan
                        await _cacheService.AddAsync<SystemInfoRecord>(hostName: record.HostName, systemInfo: record, expiry: TimeSpan.FromSeconds(30));

                        // Save record to db
                        await dataService.AddAsync(record);
                    }
                }

                _receiveChannel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);

                Debug.WriteLine(jsonMessage);

            };

            _receiveChannel.BasicConsume(queue: "monitor_service_queue", autoAck: false, consumer: createConsumer);

            return Task.CompletedTask;
        }
    }
}

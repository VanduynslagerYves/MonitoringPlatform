using Microsoft.Extensions.Options;
using MonitoringWeb.Config;
using MonitoringWeb.Model;
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

        private readonly ConnectionFactory _factory;

        public MqDataReceiver(IServiceProvider serviceProvider, IOptions<RabbitMQConfig> mqConfig)
        {
            _serviceProvider = serviceProvider;
            _factory = new ConnectionFactory
            {
                HostName = mqConfig.Value.Uri,
                Port = mqConfig.Value.Port,
                UserName = mqConfig.Value.UserName,
                Password = mqConfig.Value.Password,
            };
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (_receiveChannel == null)
            {
                try
                {
                    //try catch here foor createConnection
                    _connection = _factory.CreateConnection();
                    _receiveChannel = _connection.CreateModel();

                    _receiveChannel.QueueDeclare(queue: "monitor_service_queue",
                        durable: false, exclusive: false, autoDelete: false, arguments: null);

                    _receiveChannel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
                }
                catch (BrokerUnreachableException e)
                {
                    Debug.WriteLine(e.Message);
                }
            }

            var createConsumer = new EventingBasicConsumer(_receiveChannel);
            createConsumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var jsonMessage = Encoding.UTF8.GetString(body);

                var record = JsonSerializer.Deserialize<SystemInfoRecord>(jsonMessage);
                if(record != null)
                {
                    record.Id = Guid.NewGuid();

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dataService = scope.ServiceProvider.GetRequiredService<DataService>();
                        await dataService.Add(record); //TODO: Check use of ExecuteAsync (Entity) with a larger prefetchCount (like 20) for batch saving to db
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

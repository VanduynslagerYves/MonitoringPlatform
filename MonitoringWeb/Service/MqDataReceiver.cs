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

namespace MonitoringWeb.Service;

/// <summary>
/// Background hosted service that will process the messagequeue containing data from the clients
/// </summary>
public class MqDataReceiver : BackgroundService //TODO: place in separate background-service
{
    private IConnection? _connection;
    private IChannel? _receiveChannel;

    private readonly IServiceProvider _serviceProvider;
    private readonly ICacheService _cacheService;
    private readonly IHubContext<DataHub> _hubContext;
    private readonly ConnectionFactory _factory;

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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (_receiveChannel == null)
        {
            try
            {
                _connection = await _factory.CreateConnectionAsync(stoppingToken);
                _receiveChannel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await _receiveChannel.QueueDeclareAsync(queue: "monitor_service_queue",
                    durable: false, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);

                await _receiveChannel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);
            }
            catch (BrokerUnreachableException)
            {
                Debug.WriteLine($"RabbitMQ is unreachable: {DebugHelper.GetCurrentClassMethodAndLine()}");
            }
        }

        var systemInfoConsumer = new AsyncEventingBasicConsumer(_receiveChannel);
        systemInfoConsumer.ReceivedAsync += OnItemReceived;

        await _receiveChannel.BasicConsumeAsync(queue: "monitor_service_queue", autoAck: false, consumer: systemInfoConsumer, cancellationToken: stoppingToken);
    }

    private async Task OnItemReceived(object? sender, BasicDeliverEventArgs ea)
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

                using var scope = _serviceProvider.CreateScope();
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
                    //TODO: refactor, so the data is passed straight to the client instead of letting the client fetch the data after a notify.
                    //TODO: also do this per client data item instead of all clients => razor page code (ListItemComponent and Home) need to be refactored aswell
                    await _hubContext.Clients.All.SendAsync($"NotifyDataUpdate:all");
                }
            }

            await _receiveChannel!.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
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

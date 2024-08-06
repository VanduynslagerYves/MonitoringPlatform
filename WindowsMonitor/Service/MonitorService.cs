using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Timers;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace WindowsMonitor.Service
{
    public partial class MonitorService : ServiceBase
    {
        private Timer _timer;
        private IConnectionFactory _factory;

        public MonitorService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // Initialize RabbitMQ Connection settings
            _factory = new ConnectionFactory
            { //TODO: read from appsettings
                HostName = "127.0.0.1",
                UserName = "admin",
                Password = "root0603",
                Port = 5672,
                VirtualHost = "/"
            };

            // Initialize the timer
            _timer = new Timer();
            _timer.Interval = 5000; //milliseconds
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = true; // Ensures the timer keeps running
            _timer.Start();

            // Logging or other startup code
            EventLog.WriteEntry("Service started.");
        }

        protected override void OnStop()
        {
            // Add cleanup code here
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }

            // Logging or other shutdown code
            EventLog.WriteEntry("Service stopped.");
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                //TODO: call a (minimal) API, and let the API handle the queueing = better segregation of responsibility
                //Establish connection to RabbitMQ
                using (var connection = _factory.CreateConnection())
                {
                    //Get the message data
                    var messageData = GetMessageData(out var jsonMessage);

                    //Enqueue the data with the current connection
                    EnqueueSystemInfo(messageData, connection);

                    Debug.WriteLine("Sent: " + jsonMessage);
                }
            }
            catch (BrokerUnreachableException ex)
            {
                //Log to debug, we do not want to spam eventlog on no connection.
                Debug.WriteLine(ex.Message);
            }
            catch(Exception ex)
            {
                EventLog.WriteEntry(ex.Message);
            }
        }

        private byte[] GetMessageData(out string jsonMessage)
        {
            jsonMessage = null;
            byte[] messageBody = null;

            try
            {
                //Read system info
                var data = ReadSystemInfo();

                //Parse the data to json, to byte[]
                jsonMessage = JsonSerializer.Serialize(data);
                messageBody = Encoding.UTF8.GetBytes(jsonMessage);
            }
            catch (Exception ex) //Add CustomException for fetching data exception
            {
                throw ex;
            }

            return messageBody;
        }

        //TODO: Move to SystemInfo class
        private object ReadSystemInfo()
        {
            try
            {
                return new
                {
                    //Not frequently changing values, do not always send
                    SystemInfo.UserName,
                    SystemInfo.IP,
                    SystemInfo.OperatingSystem,
                    SystemInfo.HostName, //Add to primary key or unique via index. Or via suid (system unique id)?
                    //Frequently changing values, always send
                    SystemInfo.TimeStamp,
                    SystemInfo.CPULoad,
                    SystemInfo.UsedMemory,
                    SystemInfo.TotalMemory,
                    SystemInfo.UpTime,

                };
            }
            catch (Exception ex) //use custom exception bij ophalen data
            {
                throw ex;
            }
        }

        private void EnqueueSystemInfo(byte[] body, IConnection connection, string queueName = "monitor_service_queue")
        {
            try
            {
                using (var channel = connection.CreateModel())
                {
                    // Declare a queue (if it doesn't already exist)
                    channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);

                    // Publish the message to the queue
                    channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: null, body: body);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}

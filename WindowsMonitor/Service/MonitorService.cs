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
        private ConnectionFactory _factory;

        public MonitorService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // Initialize RabbitMQ Connection settings
            _factory = new ConnectionFactory
            { //TODO: read from appsettings
                HostName = "127.0.0.1",//"192.168.152.142",
                UserName = "admin",//"serviceuser",
                Password = "root0603",
                Port = 5672,
                VirtualHost = "/"

                /*
                192.168.152.142
                5672
                serviceuser
                honda0603  */
            };

            // Initialize the timer
            _timer = new Timer();
            _timer.Interval = 5000; //milliseconds
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = true; // Ensures the timer keeps running
            _timer.Start();

            // Logging or other startup code
            //EventLog.WriteEntry("MyService started.");
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
            //EventLog.WriteEntry("MyService stopped.");
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                //TODO: call a (minimal) API, and let the API handle the queueing = better segregation of responsibility
                //Create connection to RabbitMQ
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
            catch (Exception ex) //CustomException hier op basis van exceptions bij ophalen data
            {
                throw ex;
            }
            //Exception voor serialize error

            return messageBody;
        }

        private object ReadSystemInfo()
        {
            try
            {
                return new
                {
                    //Hieronder "niet-veranderlijke values": oplossing zoeken zodat deze niet constant verstuurd worden.
                    
                    /*
                     public static string GetMachineGuid()
                    {
                        string machineGuid = string.Empty;
                        try
                        {
                            machineGuid = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography").GetValue("MachineGuid").ToString();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error retrieving Machine GUID: " + ex.Message);
                        }
                        return machineGuid;
                    }*/
                    SystemInfo.UserName,
                    SystemInfo.IP,
                    SystemInfo.OperatingSystem,
                    SystemInfo.HostName, //is "vast" als identifier (dan nog systeem voorzien die oude records verwijderd), os unique id mogelijk?
                    //Veranderlijke waarden, worden altijd verstuurd
                    SystemInfo.TimeStamp,
                    SystemInfo.CPULoad,
                    SystemInfo.UsedMemory,
                    SystemInfo.TotalMemory,
                    SystemInfo.UpTime,

                };
                //bij starten van service alles uitlezen en verzenden, daarna enkel wijzigingen doorsturen.
                //Laatste record (enkel username, ip, os, hostnaam) hier bewaren. indien volgende record wijzigingen heeft, die wijziging mee verzenden: backend beslist dan wat er moet gebeuren (update)
                //of zoals hieronder met watchers (beter, minder info uit te lezen)
                /*
                 * private static ManagementEventWatcher watcher;

                public static void Main()
                {
                    // Set up WMI query for IP address change events
                    var query = new WqlEventQuery("SELECT * FROM __InstanceModificationEvent WITHIN 10 WHERE TargetInstance ISA 'Win32_NetworkAdapterConfiguration'");

                    // Initialize the event watcher
                    watcher = new ManagementEventWatcher(query);
                    watcher.EventArrived += OnIpAddressChanged;
                    watcher.Start();

                    Console.WriteLine("Monitoring IP address changes. Press any key to exit...");
                    Console.ReadKey();

                    watcher.Stop();
                }

                private static void OnIpAddressChanged(object sender, EventArrivedEventArgs e)
                {
                    Console.WriteLine("IP address change detected!");

                    // You can handle the IP address change here
                    // For example, retrieve the new IP address
                    var networkConfig = new ManagementObject(e.NewEvent["TargetInstance"] as ManagementBaseObject);
                    var ipAddress = networkConfig["IPAddress"] as string[];

                    if (ipAddress != null && ipAddress.Length > 0)
                    {
                        Console.WriteLine("New IP Address: " + ipAddress[0]);
                        // Perform actions based on the new IP address
                    }
                }
                 */

                /* voorbeeldschema voor nosql
                 *
                 *{
                    "hostname": "katalystpc",
                    "ip_address": "192.168.1.100",
                    "os": "windows home",
                    "username: "yves",
                    "data": [
                        {
                            "timestamp": ISODate("2024-06-20T10:00:00Z"),
                            "cpuload": 10
                        },
                        {
                            "timestamp": ISODate("2024-06-20T10:05:00Z"),
                            "cpuload": 15
                        },
                        {
                            "timestamp": ISODate("2024-06-20T10:10:00Z"),
                            "cpuload": 12
                        }
                    ]
                  }
                 */
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
                // Establish a connection to RabbitMQ server
                //using (var connection = _factory.CreateConnection())
                // Create a channel, which is where most of the API for getting things done resides
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

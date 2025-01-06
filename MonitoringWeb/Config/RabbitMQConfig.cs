namespace MonitoringWeb.Config;

public class RabbitMQConfig
{
    public required string Uri { get; set; }
    public required int Port { get; set; }
    public required string UserName { get; set; }
    public required string Password { get; set; }
}

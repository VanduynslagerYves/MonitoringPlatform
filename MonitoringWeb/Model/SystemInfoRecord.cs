namespace MonitoringWeb.Model
{
    public class SystemInfoRecord
    {
        public Guid Id { get; set; }
        public required string UserName { get; set; }
        public required string IP { get; set; }
        public required string OperatingSystem { get; set; }
        public required string HostName { get; set; }
        public required DateTime TimeStamp { get; set; }
        public required double CPULoad { get; set; }
        public required double UsedMemory { get; set; }
        public required double TotalMemory { get; set; }
        public required TimeSpan UpTime { get; set; }
    }
}

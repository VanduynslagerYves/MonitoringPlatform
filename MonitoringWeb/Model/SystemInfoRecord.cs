using System.ComponentModel.DataAnnotations;

namespace MonitoringWeb.Model
{
    public class SystemInfoRecord
    {
        [Key]
        public Guid Id { get; set; }

        [MaxLength(50)]
        public required string UserName { get; set; }

        [MaxLength(15)]
        public required string IP { get; set; }

        [MaxLength(60)]
        public required string OperatingSystem { get; set; }

        [MaxLength(63)]
        public required string HostName { get; set; }

        [Range(0,100)]
        public required double CPULoad { get; set; }

        [Range(0, 128)]
        public required double UsedMemory { get; set; }

        [Range(0,128)]
        public required double TotalMemory { get; set; }

        //[Range(0, 100)]
        //public required double MemoryLoad { get; set; }

        public required DateTime TimeStamp { get; set; }

        public required TimeSpan UpTime { get; set; }
    }
}

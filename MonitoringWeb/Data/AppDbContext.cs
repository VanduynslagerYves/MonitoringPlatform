using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MonitoringWeb.Model;

namespace MonitoringWeb.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<SystemInfoRecord> SystemInfoRecords { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SystemInfoRecord>(MapSystemInfoRecord);
        }

        public static void MapSystemInfoRecord(EntityTypeBuilder<SystemInfoRecord> sysInfoBuilder)
        {
            sysInfoBuilder.ToTable("SystemInfo");

            sysInfoBuilder.HasKey(x => x.Id).IsClustered(false);

            sysInfoBuilder.HasIndex(x => new { x.HostName, x.TimeStamp })
                .HasDatabaseName("IX_HostName_TimeStamp_Unique")
                .IsDescending().IsClustered(true).IsUnique();
        }
    }
}

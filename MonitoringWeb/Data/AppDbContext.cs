using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MonitoringWeb.Model;

namespace MonitoringWeb.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SystemInfoRecord> SystemInfoRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SystemInfoRecord>(MapSystemInfoRecord);
    }

    //~30GB disk space needed for 1 week reporting of 600 clients every 5 seconds
    public static void MapSystemInfoRecord(EntityTypeBuilder<SystemInfoRecord> sysInfoBuilder)
    {
        sysInfoBuilder.ToTable("SystemInfo");

        sysInfoBuilder.HasKey(x => x.Id).IsClustered(false);

        sysInfoBuilder.Property(x => x.UserName).HasMaxLength(50);
        //63 is the max length for a hostname in Linux, 15 for Windows
        sysInfoBuilder.Property(x => x.HostName).HasMaxLength(63);
        sysInfoBuilder.Property(x => x.IP).HasMaxLength(15);
        sysInfoBuilder.Property(x => x.OperatingSystem).HasMaxLength(60);


        sysInfoBuilder.HasIndex(x => new { x.HostName, x.TimeStamp })
            .HasDatabaseName("IX_HostName_TimeStamp_Unique")
            .IsDescending().IsClustered(true).IsUnique();
    }
}

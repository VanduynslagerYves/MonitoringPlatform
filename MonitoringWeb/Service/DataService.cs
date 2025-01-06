using Microsoft.EntityFrameworkCore;
using MonitoringWeb.Data;
using MonitoringWeb.Model;
using MonitoringWeb.Redis;
using System.Diagnostics;

namespace MonitoringWeb.Service;

public interface IDataService
{
    Task<SystemInfoRecord?> GetByName(string name);
    Task<List<SystemInfoRecord>?> GetAllAsync(int pageSize);
    Task AddAsync(SystemInfoRecord record);
}

public class DataService(IDbContextFactory<AppDbContext> contextFactory, ICacheService cacheService) : IDataService
{
    //private readonly IHubContext<DataHub> _hubContext = hubContext;
    private readonly IDbContextFactory<AppDbContext> _contextFactory = contextFactory;
    private readonly ICacheService _cacheService = cacheService;

    public async Task<SystemInfoRecord?> GetByName(string name)
    {
        var record = await _cacheService.GetAsync<SystemInfoRecord>(name);
        if (record != null) return record;

        using var context = await _contextFactory.CreateDbContextAsync();
        record = await context.SystemInfoRecords.FirstOrDefaultAsync(x => x.HostName.Equals(name));

        return record;
    }

    public async Task<List<SystemInfoRecord>?> GetAllAsync(int pageSize)
    {
        Debug.Write("Reading from database");
        // If no items in cache, read from database
        using var context = await _contextFactory.CreateDbContextAsync();
        var records = await context.SystemInfoRecords.AsNoTracking()
        // Group all records by Hostname
        .GroupBy(r => r.HostName)
        // For each group, select the Hostname and its maximum TimeStamp
        .Select(g => new
        {
            HostName = g.Key,
            MaxTimestamp = g.Max(i => i.TimeStamp)
        })
        // Join the result back to the original SystemInfoRecords table
        .Join(
            context.SystemInfoRecords,
            // The outer sequence: our grouped results with max timestamps
            max => new { max.HostName, max.MaxTimestamp },
            // The inner sequence: the original SystemInfoRecords table
            record => new { record.HostName, MaxTimestamp = record.TimeStamp },
            // The result selector: return the full record from SystemInfoRecords
            (max, record) => record
        )
        .OrderBy(r => r.HostName)
        // Execute the query and return results as a list
        .ToListAsync();

        return records;
    }

    public async Task AddAsync(SystemInfoRecord record)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        try
        {
            await context.SystemInfoRecords.AddAsync(record);
            await context.SaveChangesAsync();
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
        }

        ////notify clients of model change, the client will fetch the new data
        //await _hubContext.Clients.All.SendAsync($"NotifyDataUpdate:all");
    }
}

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MonitoringWeb.Data;
using MonitoringWeb.Hubs;
using MonitoringWeb.Model;
using MonitoringWeb.Redis;
using System.Diagnostics;

namespace MonitoringWeb.Service
{
    public interface IDataService
    {
        Task<SystemInfoRecord?> GetByName(string name);
        Task<List<SystemInfoRecord>> GetAllAsync();
        Task AddAsync(SystemInfoRecord record);
    }

    public class DataService : IDataService
    {
        private readonly IHubContext<DataHub> _hubContext;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ICacheService _cacheService;

        public DataService(IDbContextFactory<AppDbContext> contextFactory, IHubContext<DataHub> hubContext, ICacheService cacheService)
        {
            _contextFactory = contextFactory;
            _hubContext = hubContext;
            _cacheService = cacheService;
        }

        public async Task<SystemInfoRecord?> GetByName(string name)
        {
            var record = await _cacheService.GetAsync<SystemInfoRecord>(name);
            if (record != null) return record;

            using var context = await _contextFactory.CreateDbContextAsync();
            record = await context.SystemInfoRecords.FirstOrDefaultAsync(x => x.HostName.Equals(name));

            return record;
        }

        public async Task<List<SystemInfoRecord>> GetAllAsync()
        {
            // Read from cache
            var groupedCacheRecords = await _cacheService.GetAllAsync<SystemInfoRecord>();
            if (groupedCacheRecords.Values.Any())
            {
                Debug.WriteLine("Reading from cache");
                //cacheList = groupedCacheRecords.Values.OrderBy(x => x.HostName).ToList();
                return groupedCacheRecords.Values.OrderBy(x => x.HostName).ToList();
            }

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

            await context.SystemInfoRecords.AddAsync(record);
            await context.SaveChangesAsync();

            //notify clients of model change, the client will fetch the new data
            await _hubContext.Clients.All.SendAsync($"NotifyDataUpdate:all");
        }
    }
}

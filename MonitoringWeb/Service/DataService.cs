using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MonitoringWeb.Data;
using MonitoringWeb.Hubs;
using MonitoringWeb.Model;
using System.Drawing;

namespace MonitoringWeb.Service
{
    public class DataService
    {
        private readonly IHubContext<DataHub> _hubContext;

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public DataService(IDbContextFactory<AppDbContext> contextFactory, IHubContext<DataHub> hubContext)
        {
            _contextFactory = contextFactory;
            _hubContext = hubContext;
        }

        /// <summary>
        /// Groups records by HostName, but doesn't load all records into memory.
        /// For each group, it selects only the Hostname and the maximum TimeStamp.This operation is done in the database, reducing the amount of data transferred.
        /// It then joins this result (which contains only Hostnames and their latest TimeStamps) back to the original SystemInfoRecords table.
        /// The join matches records where both the Hostname and TimeStamp match, effectively selecting the latest record for each Hostname.
        /// Finally, it returns the full SystemInfoRecord for each of these matches.
        /// TODO: get the latest records from cache
        /// </summary>
        /// <returns></returns>
        public async Task<List<SystemInfoRecord>> GetAllAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var records = await context.SystemInfoRecords
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
            // Execute the query and return results as a list
            .ToListAsync();

            return records;
        }

        public async Task<SystemInfoRecord?> GetLastAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var last = await context.SystemInfoRecords.OrderBy(i => i.TimeStamp).LastOrDefaultAsync();
            return last;
        }

        public async Task Add(SystemInfoRecord record)
        {
            //TODO: add to cache
            using var context = await _contextFactory.CreateDbContextAsync();
            context.SystemInfoRecords.Add(record);
            await context.SaveChangesAsync();

            //notify clients of model change, the client will fetch the new data
            await _hubContext.Clients.All.SendAsync("ReceiveDataUpdate");
        }
    }
}

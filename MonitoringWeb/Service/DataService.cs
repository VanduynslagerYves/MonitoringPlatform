using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MonitoringWeb.Data;
using MonitoringWeb.Hubs;
using MonitoringWeb.Model;

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

        public async Task<List<SystemInfoRecord>> GetAllAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.SystemInfoRecords.OrderBy(i => i.TimeStamp).ToListAsync();
        }

        public async Task<SystemInfoRecord?> GetLastAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var last = await context.SystemInfoRecords.OrderBy(i => i.TimeStamp).LastOrDefaultAsync();
            return last;
        }

        public async Task Add(SystemInfoRecord record)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.SystemInfoRecords.Add(record);
            await context.SaveChangesAsync();

            //notify clients of model change, the client will fetch the new data
            await _hubContext.Clients.All.SendAsync("ReceiveDataUpdate");
        }
    }
}

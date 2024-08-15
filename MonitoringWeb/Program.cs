using Microsoft.EntityFrameworkCore;
using MonitoringWeb.Components;
using MonitoringWeb.Config;
using MonitoringWeb.Data;
using MonitoringWeb.Hubs;
using MonitoringWeb.Redis;
using MonitoringWeb.Service;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContextFactory<AppDbContext>(
    options => options.UseSqlServer(builder.Configuration.GetConnectionString("DbContextConnection")));

builder.Services.AddSingleton<RedisConnection>();
builder.Services.AddScoped<IDataService, DataService>();
builder.Services.AddSingleton<ICacheService, CacheService>();

builder.Services.AddHostedService<MqDataReceiver>();

builder.Services.Configure<RabbitMQConfig>(builder.Configuration.GetSection("RabbitMQConfig"));

builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.MapHub<DataHub>("/datahub");

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

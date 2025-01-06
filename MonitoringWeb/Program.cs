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

builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache(); // For in-memory session state
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

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

app.UseSession();

//begin SetString() hack
//https://stackoverflow.com/questions/60660923/session-setstring-in-server-side-net-core-produces-error-the-session-cannot
app.Use(async delegate (HttpContext Context, Func<Task> Next)
{
    //this throwaway session variable will "prime" the SetString() method
    //to allow it to be called after the response has started
    var TempKey = Guid.NewGuid().ToString(); //create a random key
    Context.Session.Set(TempKey, []); //set the throwaway session variable
    Context.Session.Remove(TempKey); //remove the throwaway session variable
    await Next(); //continue on with the request
});
//end SetString() hack

app.Run();

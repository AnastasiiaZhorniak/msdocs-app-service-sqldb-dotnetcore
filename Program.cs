using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;

var builder = WebApplication.CreateBuilder(args);

// --------------------
// Azure SQL Database
// --------------------
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<MyDatabaseContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("MyDbConnection")));
}
else
{
    builder.Services.AddDbContext<MyDatabaseContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("AZURE_SQL_CONNECTIONSTRING")));
}

// --------------------
// Azure Managed Redis
// --------------------
var redisConnectionString =
    builder.Configuration["AZURE_REDIS_CONNECTIONSTRING"];

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "TodoApp:";
    });
}
else
{
    // Fallback для локального запуску або якщо Redis не налаштований
    builder.Services.AddDistributedMemoryCache();
}

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add App Service logging
builder.Logging.AddAzureWebAppDiagnostics();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Todos}/{action=Index}/{id?}");

app.Run();

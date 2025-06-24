using GolfClubServer.Context;
using GolfClubServer.Data;
using GolfClubServer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/log-.txt", 
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    // Add services to the container.
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "GolfClub API",
            Version = "v1",
            Description = "API for GolfClub System"
        });
        // Optional: Include XML comments if you have them
        // var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        // var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
        // c.IncludeXmlComments(xmlPath);
    });
    
    // Add DbContext
    builder.Services.AddDbContext<MyDbContext>(options =>
        options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
            ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))));
    
    // Register UnitOfWork (scoped)
    builder.Services.AddScoped<UnitOfWork>();
    
    // Register HttpClientFactory
    builder.Services.AddHttpClient();

    // Register TelegramService as singleton and hosted service
    builder.Services.AddSingleton<TelegramService>(sp => new TelegramService(sp, builder.Configuration["Telegram:BotToken"]));
    
    // Background services
    builder.Services.AddHostedService(sp => sp.GetRequiredService<TelegramService>());
    builder.Services.AddHostedService<TrackingService>();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage(); 
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}
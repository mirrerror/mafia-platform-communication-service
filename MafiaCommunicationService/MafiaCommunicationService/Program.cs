using DotNetEnv;
using MafiaCommunicationService.Data;
using MafiaCommunicationService.Filters;
using MafiaCommunicationService.Hubs;
using MafiaCommunicationService.Middleware;
using MafiaCommunicationService.Services;
using MafiaCommunicationService.Protos; 
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Serilog;
using System.Net;

Env.Load(options: LoadOptions.TraversePath());

var logPath = Environment.GetEnvironmentVariable("LOG_FILE_PATH") ?? "logs/service.log";

var builder = WebApplication.CreateBuilder(args);

var restPortStr = Environment.GetEnvironmentVariable("SERVICE_PORT") ?? "8080";
if (!int.TryParse(restPortStr, out var restPort)) restPort = 8080;

var rpcPortStr = Environment.GetEnvironmentVariable("RPC_PORT") ?? "6000";
if (!int.TryParse(rpcPortStr, out var rpcPort)) rpcPort = 6000;

builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Any, restPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;
    });

    options.Listen(IPAddress.Any, rpcPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.File(logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        shared: true,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

var maxConcurrentRequestsStr = Environment.GetEnvironmentVariable("MAX_CONCURRENT_REQUESTS") ?? "100";
if (!int.TryParse(maxConcurrentRequestsStr, out var maxConcurrentRequests))
{
    maxConcurrentRequests = 100;
}

builder.Services.AddSingleton(new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests));
builder.Services.AddSingleton<IHubFilter, ThrottlingHubFilter>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policyBuilder =>
    {
        policyBuilder.SetIsOriginAllowed(_ => true).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
    });
});

builder.Services.AddHttpClient();

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

var discoveryUrl = Environment.GetEnvironmentVariable("DISCOVERY_SERVICE_GRPC_URL");
if (!string.IsNullOrEmpty(discoveryUrl))
{
    builder.Services.AddGrpcClient<RegistrationService.RegistrationServiceClient>(o =>
    {
        o.Address = new Uri(discoveryUrl);
    })
    .ConfigureChannel(o =>
    {
        o.HttpHandler = new SocketsHttpHandler
        {
            EnableMultipleHttp2Connections = true,
            KeepAlivePingDelay = TimeSpan.FromSeconds(60),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(30)
        };
    });
}

builder.Services.AddSingleton<ServiceRegistryClient>();

builder.Services.AddSignalR();
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var firstErrorMessage = context.ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid request.";
            var errorResponse = new { code = "VALIDATION_ERROR", message = firstErrorMessage };
            return new BadRequestObjectResult(errorResponse);
        };
    });

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions => 
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5, 
            maxRetryDelay: TimeSpan.FromSeconds(2), 
            errorCodesToAdd: null);
    }));

builder.Services.AddScoped<IChatService, PostgresChatService>();

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found."));

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
var registryClient = app.Services.GetRequiredService<ServiceRegistryClient>();

app.UseSerilogRequestLogging();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<ChatDbContext>();
        if (dbContext.Database.IsRelational())
        {
            logger.LogInformation("Attempting to migrate database...");
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Database migration completed successfully.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

app.Lifetime.ApplicationStarted.Register(async void () =>
{
    logger.LogInformation("Application started. Registering with service discovery...");
    await registryClient.RegisterAsync();
});

app.Lifetime.ApplicationStopping.Register(async void () =>
{
    logger.LogInformation("Application stopping. Deregistering from service discovery...");
    await registryClient.DeregisterAsync();
});

app.UseCors("CorsPolicy");
app.UseRouting();

app.UseMetricServer();
app.UseHttpMetrics();

app.UseMiddleware<RequestThrottlingMiddleware>();

app.MapControllers();
app.MapHub<ChatHub>("/chathub");

app.MapGrpcService<GrpcSubscriberService>();
app.MapGrpcReflectionService();

app.MapHealthChecks("/healthz");

try
{
    var logDir = Path.GetDirectoryName(logPath);
    if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
    {
        Directory.CreateDirectory(logDir);
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to create log directory at: {LogPath}", logPath);
}

app.Run();
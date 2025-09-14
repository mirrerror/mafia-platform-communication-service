using DotNetEnv;
using MafiaCommunicationService.Data;
using MafiaCommunicationService.Hubs;
using MafiaCommunicationService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

Env.Load(options: LoadOptions.TraversePath());

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policyBuilder =>
    {
        policyBuilder.SetIsOriginAllowed(_ => true).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
    });
});

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
    options.UseNpgsql(connectionString));

// builder.Services.AddSingleton<IChatService, InMemoryChatService>();
builder.Services.AddScoped<IChatService, PostgresChatService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<ChatDbContext>();
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

app.UseCors("CorsPolicy");
app.UseRouting();
app.MapControllers();
app.MapHub<ChatHub>("/chathub");

app.Run();


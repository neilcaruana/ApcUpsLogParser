using ApcUpsLogParser.Common.Services;
using ApcUpsLogParser.Web.Hubs;
using ApcUpsLogParser.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Check if running under IIS
var isIIS = Environment.GetEnvironmentVariable("APP_POOL_ID") != null;
var isDevelopment = builder.Environment.IsDevelopment();

DataLogFilePrompt.EnsureDataLogFileExists(builder.Configuration, builder.Environment.EnvironmentName);

if (!isIIS && !isDevelopment)
{
    // Only configure Kestrel for self-hosted mode (not when launched from Visual Studio)
    builder.WebHost.ConfigureKestrel(options =>
    {
        // Try multiple ports in case one is in use
        var ports = new[] { 5000, 5001, 8080, 8081, 3000 };
        var selectedPort = 5000; // Default

        // Check if a specific port was requested via command line
        if (args.Length > 0 && args[0].StartsWith("--port="))
        {
            if (int.TryParse(args[0].Substring(7), out int requestedPort))
            {
                selectedPort = requestedPort;
            }
        }
        else
        {
            // Find an available port
            foreach (var port in ports)
            {
                try
                {
                    var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
                    listener.Start();
                    listener.Stop();
                    selectedPort = port;
                    break;
                }
                catch
                {
                    continue; // Port in use, try next
                }
            }
        }

        // Configure Kestrel to listen on the selected port
        options.ListenLocalhost(selectedPort);

        // Store the port for later use
        Environment.SetEnvironmentVariable("SELECTED_PORT", selectedPort.ToString());
    });
}

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddHostedService<FileWatcherService>();

// Add console logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

// Get the selected port (only relevant for self-hosted)
var selectedPort = Environment.GetEnvironmentVariable("SELECTED_PORT") ?? "unknown";

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    // Enable OpenAPI/Swagger in all environments for this monitoring app
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "APC UPS Log Parser API v1");
        options.RoutePrefix = "swagger"; // Access at /swagger
    });
}

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        ctx.Context.Response.Headers["Pragma"] = "no-cache";
        ctx.Context.Response.Headers["Expires"] = "0";
    }
});
app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapHub<VoltageHub>("/hubs/voltage");
app.MapFallbackToFile("index.html");

// Display startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("===========================================");
if (isIIS)
{
    logger.LogInformation("APC UPS Log Parser - IIS Hosted");
}
else
{
    logger.LogInformation("APC UPS Log Parser - Self-Hosted");
    logger.LogInformation("HTTP Server: http://localhost:{Port}", selectedPort);
}
logger.LogInformation("SignalR Hub: /hubs/voltage");
logger.LogInformation("Swagger UI: /swagger");
logger.LogInformation("===========================================");

// Handle startup errors gracefully
try
{
    app.Run();
}
catch (IOException ex) when (ex.Message.Contains("Failed to bind"))
{
    logger.LogError("Port binding failed. Try running with a different port:");
    logger.LogError("Example: dotnet run --port=8080");
    logger.LogError("Or run as Administrator if using port 1006");
    throw;
}
using BroadcastOverlay;
using SVappsLAB.iRacingTelemetrySDK;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddSignalR();

// Configure CORS to allow overlay clients from different origins
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",    // React dev server
                "http://localhost:5173",    // Vite dev server
                "http://127.0.0.1:3000",
                "http://127.0.0.1:5173"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Add telemetry backend as hosted service
builder.Services.AddHostedService<TelemetryBackendService>();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors();

// Map SignalR hub
app.MapHub<BroadcastHub>("/broadcast");

// Serve static files for overlay HTML
app.UseDefaultFiles();
app.UseStaticFiles();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// Info endpoint
app.MapGet("/", () => Results.Ok(new 
{ 
    service = "iRacing Broadcast Overlay Backend",
    version = "1.0.0",
    signalrHub = "/broadcast",
    overlayUrl = "/overlay.html",
    producerPanelUrl = "/producer.html"
}));

app.Logger.LogInformation("Broadcast overlay backend starting on {Urls}", 
    string.Join(", ", builder.WebHost.GetSetting("urls")?.Split(';') ?? new[] { "http://localhost:5000" }));

app.Logger.LogInformation("SignalR hub available at: /broadcast");
app.Logger.LogInformation("Overlay HTML will be available at: /overlay.html");
app.Logger.LogInformation("Producer panel HTML will be available at: /producer.html");

app.Run();

// Define required telemetry variables for code generation
// This marker class triggers the source generator to create TelemetryData struct
[RequiredTelemetryVars([
    // Position and timing data for standings
    TelemetryVar.CarIdxPosition,
    TelemetryVar.CarIdxClassPosition,
    TelemetryVar.CarIdxLap,
    TelemetryVar.CarIdxLapDistPct,
    TelemetryVar.CarIdxF2Time,
    TelemetryVar.CarIdxEstTime,
    
    // Lap times
    TelemetryVar.CarIdxLastLapTime,
    TelemetryVar.CarIdxBestLapTime,
    
    // Status indicators
    TelemetryVar.CarIdxOnPitRoad,
    TelemetryVar.CarIdxTrackSurface,
    TelemetryVar.CarIdxClass,
    
    // Session info
    TelemetryVar.SessionNum,
    TelemetryVar.SessionState,
    TelemetryVar.SessionFlags,
    TelemetryVar.SessionTime,
    TelemetryVar.SessionTimeRemain,
    TelemetryVar.SessionLapsRemain
])]
internal class TelemetryDataDefinition { }

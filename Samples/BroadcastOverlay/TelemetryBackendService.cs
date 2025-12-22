using Microsoft.AspNetCore.SignalR;
using SVappsLAB.iRacingTelemetrySDK;

namespace BroadcastOverlay;

/// <summary>
/// Background service that connects to iRacing, processes telemetry data,
/// and broadcasts it to connected overlay clients via SignalR
/// </summary>
public class TelemetryBackendService : BackgroundService
{
    private readonly IHubContext<BroadcastHub> _hubContext;
    private readonly ILogger<TelemetryBackendService> _logger;
    private readonly Dictionary<int, Driver> _driverCache = new();
    private TelemetrySessionInfo? _lastSessionInfo;

    public TelemetryBackendService(
        IHubContext<BroadcastHub> hubContext,
        ILogger<TelemetryBackendService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telemetry backend service starting...");

        try
        {
            // Create telemetry client (will connect to live iRacing or use IBT file if specified)
            // For IBT file support, modify this to check command line args
            await using var client = TelemetryClient<TelemetryData>.Create(_logger);
            
            _logger.LogInformation("Telemetry client created, starting monitor...");

            // Start processing tasks
            var telemetryTask = ProcessTelemetryStream(client, stoppingToken);
            var sessionTask = ProcessSessionStream(client, stoppingToken);
            var monitorTask = client.Monitor(stoppingToken);
            
            await Task.WhenAny(monitorTask, telemetryTask, sessionTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in telemetry backend service");
            throw;
        }
    }

    private async Task ProcessTelemetryStream(
        ITelemetryClient<TelemetryData> client, 
        CancellationToken cancellationToken)
    {
        var frameCounter = 0;
        
        _logger.LogInformation("Starting telemetry stream processing...");

        await foreach (var data in client.TelemetryData.WithCancellation(cancellationToken))
        {
            // Throttle to 10Hz for overlay updates (every 6th frame at 60Hz)
            // This reduces network traffic while maintaining smooth visuals
            if (++frameCounter % 6 != 0) continue;
            
            try
            {
                var broadcastData = CreateBroadcastData(data);
                
                // Send to all connected overlay clients
                await _hubContext.Clients.All.SendAsync(
                    "TelemetryUpdate", 
                    broadcastData, 
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting telemetry update");
            }
        }
    }

    private async Task ProcessSessionStream(
        ITelemetryClient<TelemetryData> client, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting session stream processing...");

        await foreach (var session in client.SessionData.WithCancellation(cancellationToken))
        {
            try
            {
                _lastSessionInfo = session;

                // Update driver cache with new information
                if (session.DriverInfo?.Drivers != null)
                {
                    var newDrivers = new List<object>();
                    
                    foreach (var driver in session.DriverInfo.Drivers)
                    {
                        var isNew = !_driverCache.ContainsKey(driver.CarIdx);
                        _driverCache[driver.CarIdx] = driver;
                        
                        if (isNew)
                        {
                            newDrivers.Add(new
                            {
                                driver.CarIdx,
                                driver.UserName,
                                driver.CarNumber,
                                driver.TeamName,
                                driver.CarScreenName,
                                driver.IRating,
                                driver.LicString,
                                driver.CarClassID,
                                driver.CarClassShortName
                            });
                        }
                    }

                    if (newDrivers.Any())
                    {
                        _logger.LogInformation("Broadcasting {Count} new/updated drivers", newDrivers.Count);
                        await _hubContext.Clients.All.SendAsync(
                            "DriversUpdate", 
                            newDrivers, 
                            cancellationToken);
                    }
                }

                // Broadcast session information
                var sessionData = new
                {
                    TrackName = session.WeekendInfo?.TrackDisplayName,
                    TrackConfig = session.WeekendInfo?.TrackConfigName,
                    TrackLength = session.WeekendInfo?.TrackLength,
                    SessionType = session.SessionInfo?.Sessions?.FirstOrDefault()?.SessionType,
                    DriverCount = session.DriverInfo?.Drivers?.Count ?? 0
                };
                
                _logger.LogInformation("Session update: {Track} - {Session} - {Drivers} drivers", 
                    sessionData.TrackName, sessionData.SessionType, sessionData.DriverCount);

                await _hubContext.Clients.All.SendAsync(
                    "SessionUpdate", 
                    sessionData, 
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting session update");
            }
        }
    }

    private object CreateBroadcastData(TelemetryData data)
    {
        // Process and format data for broadcast
        var standings = new List<object>();
        
        if (data.CarIdxPosition != null && data.CarIdxPosition.Length > 0)
        {
            for (int i = 0; i < data.CarIdxPosition.Length; i++)
            {
                var position = data.CarIdxPosition[i];
                if (position <= 0) continue; // Skip inactive cars
                
                standings.Add(new
                {
                    CarIdx = i,
                    Position = position,
                    ClassPosition = data.CarIdxClassPosition?[i],
                    Lap = data.CarIdxLap?[i],
                    LapPct = data.CarIdxLapDistPct?[i],
                    GapToLeader = data.CarIdxF2Time?[i],
                    EstTime = data.CarIdxEstTime?[i],
                    LastLapTime = data.CarIdxLastLapTime?[i],
                    BestLapTime = data.CarIdxBestLapTime?[i],
                    OnPitRoad = data.CarIdxOnPitRoad?[i] ?? false,
                    TrackSurface = data.CarIdxTrackSurface?[i],
                    CarClass = data.CarIdxClass?[i]
                });
            }
        }
        
        return new
        {
            Standings = standings.OrderBy(s => ((dynamic)s).Position).ToList(),
            SessionNum = data.SessionNum,
            SessionState = data.SessionState,
            SessionFlags = data.SessionFlags,
            SessionTime = data.SessionTime,
            SessionTimeRemain = data.SessionTimeRemain,
            SessionLapsRemain = data.SessionLapsRemain,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
}

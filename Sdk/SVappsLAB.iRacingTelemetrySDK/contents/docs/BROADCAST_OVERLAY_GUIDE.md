# Building Broadcast Overlays with iRacing Telemetry SDK

> **Guide Overview**: This document provides comprehensive guidance for building professional broadcast overlay systems using the iRacing Telemetry SDK, including standings towers, battle boxes, timing displays, and other broadcast graphics controlled by a separate producer panel application.

## Table of Contents

- [Overview](#overview)
- [Architecture Patterns](#architecture-patterns)
- [Key Components](#key-components)
- [Implementation Guide](#implementation-guide)
- [Example Code](#example-code)
- [Best Practices](#best-practices)
- [Performance Considerations](#performance-considerations)

## Overview

### What is a Broadcast Overlay System?

A broadcast overlay system consists of:

1. **Telemetry Backend Service**: Connects to iRacing, processes telemetry data, and distributes it to connected clients
2. **Producer Panel Application**: Control interface for broadcast operators to manage what's displayed and how
3. **Overlay Renderer**: Browser-based or native application displaying graphics on screen (OBS, vMix, etc.)

### Why Use This SDK for Broadcast Overlays?

- **Real-time Performance**: 60Hz telemetry updates ensure smooth, responsive graphics
- **Rich Data Access**: 200+ telemetry variables including positions, timing, speeds, incidents, and more
- **Flexible Architecture**: Async streaming enables efficient multi-client distribution
- **Type Safety**: Strongly-typed data structures reduce errors in production environments
- **Multi-car Data**: Built-in support for tracking all cars in the session simultaneously

## Architecture Patterns

### Pattern 1: Simple WebSocket Broadcast

```
┌─────────────────────────────────────────────────────────────┐
│                     iRacing Simulator                       │
│                  (Live 60Hz Telemetry)                      │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│            Telemetry Backend Service (.NET)                 │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  ITelemetryClient<TelemetryData>                     │   │
│  │  • Connects to iRacing                               │   │
│  │  • Processes telemetry at 60Hz                       │   │
│  │  • Calculates positions, gaps, standings             │   │
│  └──────────────────┬───────────────────────────────────┘   │
│                     │                                        │
│  ┌──────────────────▼───────────────────────────────────┐   │
│  │  SignalR/WebSocket Hub                               │   │
│  │  • Broadcasts processed data                         │   │
│  │  • Manages client connections                        │   │
│  │  • Throttles updates (10-30Hz typical)               │   │
│  └──────────────────┬───────────────────────────────────┘   │
└────────────────────┬┴───────────────────────────────────────┘
                     │
          ┌──────────┴──────────┐
          ▼                     ▼
┌─────────────────────┐  ┌──────────────────────┐
│  Producer Panel     │  │  Overlay Renderer    │
│  (Web/Desktop App)  │  │  (Browser/OBS)       │
│  • Control UI       │  │  • Standings Tower   │
│  • Scene selection  │  │  • Battle Boxes      │
│  • Data filtering   │  │  • Timing Display    │
│  • Manual overrides │  │  • Custom Graphics   │
└─────────────────────┘  └──────────────────────┘
```

### Pattern 2: Separated Producer Control

```
┌─────────────────────────────────────────────────────────────┐
│                     iRacing Simulator                       │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│            Telemetry Backend Service                        │
│  • Processes telemetry                                      │
│  • Maintains state                                          │
│  • SignalR Hub for telemetry broadcasts                     │
└──────────────────────┬──────────────────────────────────────┘
                       │
          ┌────────────┼────────────┐
          │            │            │
          ▼            ▼            ▼
    ┌─────────┐  ┌─────────┐  ┌─────────┐
    │Producer │  │Overlay 1│  │Overlay 2│
    │ Panel   │  │(Graphic)│  │(Graphic)│
    └────┬────┘  └────┬────┘  └────┬────┘
         │            │            │
         └────────────┴────────────┘
         Control messages flow back
         to configure overlays
```

## Key Components

### 1. Telemetry Variables for Broadcast Graphics

#### Standings Tower Data
```csharp
[RequiredTelemetryVars([
    // Position and timing
    TelemetryVar.CarIdxPosition,           // Race position
    TelemetryVar.CarIdxClassPosition,      // Class position
    TelemetryVar.CarIdxLap,                // Current lap
    TelemetryVar.CarIdxLapDistPct,         // Position on track
    TelemetryVar.CarIdxF2Time,             // Time behind leader
    
    // Multi-class racing
    TelemetryVar.CarIdxClass,              // Car class
    
    // Status indicators
    TelemetryVar.CarIdxOnPitRoad,          // In pit lane
    TelemetryVar.CarIdxTrackSurface,       // On track status
    
    // Session info
    TelemetryVar.SessionNum,
    TelemetryVar.SessionState,
    TelemetryVar.SessionFlags
])]
```

#### Battle Box Data
```csharp
[RequiredTelemetryVars([
    // Speed and performance
    TelemetryVar.Speed,
    TelemetryVar.CarIdxLapDistPct,
    
    // Gaps between cars
    TelemetryVar.CarIdxF2Time,
    TelemetryVar.CarIdxEstTime,
    
    // Last lap times
    TelemetryVar.CarIdxLastLapTime,
    TelemetryVar.CarIdxBestLapTime,
    
    // Positions
    TelemetryVar.CarIdxPosition,
    TelemetryVar.CarIdxClassPosition
])]
```

#### Timing and Scoring
```csharp
[RequiredTelemetryVars([
    // Lap timing
    TelemetryVar.LapCurrentLapTime,
    TelemetryVar.LapLastLapTime,
    TelemetryVar.LapBestLapTime,
    TelemetryVar.CarIdxLastLapTime,
    TelemetryVar.CarIdxBestLapTime,
    
    // Sector times
    TelemetryVar.LapDeltaToBestLap,
    TelemetryVar.LapDeltaToSessionBestLap,
    
    // Session timing
    TelemetryVar.SessionTime,
    TelemetryVar.SessionTimeRemain,
    TelemetryVar.SessionLapsRemain
])]
```

### 2. Session Info for Driver Details

The `TelemetrySessionInfo` object provides driver details needed for overlays:

```csharp
// Access driver information from session info
var driverInfo = sessionInfo.DriverInfo;
foreach (var driver in driverInfo.Drivers)
{
    var driverName = driver.UserName;
    var carNumber = driver.CarNumber;
    var carName = driver.CarScreenName;
    var teamName = driver.TeamName;
    var iRating = driver.IRating;
    var licenseLevel = driver.LicString;
    var carClassId = driver.CarClassID;
}
```

## Implementation Guide

### Step 1: Create the Telemetry Backend Service

```csharp
using Microsoft.AspNetCore.SignalR;
using SVappsLAB.iRacingTelemetrySDK;

[RequiredTelemetryVars([
    TelemetryVar.CarIdxPosition,
    TelemetryVar.CarIdxClassPosition,
    TelemetryVar.CarIdxLap,
    TelemetryVar.CarIdxLapDistPct,
    TelemetryVar.CarIdxF2Time,
    TelemetryVar.CarIdxLastLapTime,
    TelemetryVar.CarIdxBestLapTime,
    TelemetryVar.CarIdxOnPitRoad,
    TelemetryVar.CarIdxTrackSurface,
    TelemetryVar.SessionNum,
    TelemetryVar.SessionState,
    TelemetryVar.SessionFlags
])]
public class TelemetryBackendService : BackgroundService
{
    private readonly IHubContext<BroadcastHub> _hubContext;
    private readonly ILogger<TelemetryBackendService> _logger;
    
    public TelemetryBackendService(
        IHubContext<BroadcastHub> hubContext,
        ILogger<TelemetryBackendService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var client = TelemetryClient<TelemetryData>.Create(_logger);
        
        var telemetryTask = ProcessTelemetryStream(client, stoppingToken);
        var sessionTask = ProcessSessionStream(client, stoppingToken);
        var monitorTask = client.Monitor(stoppingToken);
        
        await Task.WhenAny(monitorTask, telemetryTask, sessionTask);
    }

    private async Task ProcessTelemetryStream(
        ITelemetryClient<TelemetryData> client, 
        CancellationToken cancellationToken)
    {
        var frameCounter = 0;
        
        await foreach (var data in client.TelemetryData.WithCancellation(cancellationToken))
        {
            // Throttle to 10Hz for overlay updates (every 6th frame at 60Hz)
            if (++frameCounter % 6 != 0) continue;
            
            var broadcastData = CreateBroadcastData(data);
            
            // Send to all connected overlay clients
            await _hubContext.Clients.All.SendAsync(
                "TelemetryUpdate", 
                broadcastData, 
                cancellationToken);
        }
    }

    private async Task ProcessSessionStream(
        ITelemetryClient<TelemetryData> client, 
        CancellationToken cancellationToken)
    {
        await foreach (var session in client.SessionData.WithCancellation(cancellationToken))
        {
            var sessionData = new
            {
                TrackName = session.WeekendInfo?.TrackDisplayName,
                SessionType = session.SessionInfo?.Sessions?[0]?.SessionType,
                Drivers = session.DriverInfo?.Drivers?.Select(d => new
                {
                    d.CarIdx,
                    d.UserName,
                    d.CarNumber,
                    d.TeamName,
                    d.CarScreenName,
                    d.IRating,
                    d.LicString,
                    d.CarClassID
                })
            };
            
            await _hubContext.Clients.All.SendAsync(
                "SessionUpdate", 
                sessionData, 
                cancellationToken);
        }
    }

    private object CreateBroadcastData(TelemetryData data)
    {
        // Process and format data for broadcast
        var standings = new List<object>();
        
        if (data.CarIdxPosition != null)
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
                    LastLapTime = data.CarIdxLastLapTime?[i],
                    BestLapTime = data.CarIdxBestLapTime?[i],
                    OnPitRoad = data.CarIdxOnPitRoad?[i] ?? false,
                    TrackSurface = data.CarIdxTrackSurface?[i]
                });
            }
        }
        
        return new
        {
            Standings = standings.OrderBy(s => ((dynamic)s).Position).ToList(),
            SessionNum = data.SessionNum,
            SessionState = data.SessionState,
            SessionFlags = data.SessionFlags,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
}
```

### Step 2: Create the SignalR Hub

```csharp
using Microsoft.AspNetCore.SignalR;

public class BroadcastHub : Hub
{
    private readonly ILogger<BroadcastHub> _logger;
    
    public BroadcastHub(ILogger<BroadcastHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // Producer panel can send control commands
    public async Task UpdateOverlaySettings(string settingName, object value)
    {
        _logger.LogInformation("Overlay setting updated: {Setting} = {Value}", 
            settingName, value);
        
        // Broadcast setting change to all overlay clients
        await Clients.Others.SendAsync("SettingChanged", settingName, value);
    }

    // Subscribe to specific data streams
    public async Task SubscribeToStandings()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "Standings");
        _logger.LogInformation("Client subscribed to standings: {ConnectionId}", 
            Context.ConnectionId);
    }

    public async Task SubscribeToBattleBox()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "BattleBox");
        _logger.LogInformation("Client subscribed to battle box: {ConnectionId}", 
            Context.ConnectionId);
    }
}
```

### Step 3: Configure ASP.NET Core Web API

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add telemetry backend as hosted service
builder.Services.AddHostedService<TelemetryBackendService>();

var app = builder.Build();

// Configure middleware
app.UseCors();
app.MapHub<BroadcastHub>("/broadcast");

app.Run();
```

### Step 4: Create the Overlay Client (HTML/JavaScript)

```html
<!DOCTYPE html>
<html>
<head>
    <title>iRacing Broadcast Overlay - Standings Tower</title>
    <style>
        body {
            margin: 0;
            padding: 20px;
            background: transparent;
            font-family: 'Roboto', Arial, sans-serif;
            color: white;
        }
        
        .standings-tower {
            position: absolute;
            top: 20px;
            right: 20px;
            background: rgba(0, 0, 0, 0.8);
            border-radius: 8px;
            padding: 10px;
            min-width: 300px;
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.5);
        }
        
        .standings-header {
            font-size: 18px;
            font-weight: bold;
            padding: 8px;
            border-bottom: 2px solid #00ff00;
            margin-bottom: 10px;
        }
        
        .driver-row {
            display: grid;
            grid-template-columns: 40px 50px 1fr 80px 80px;
            gap: 8px;
            padding: 6px 8px;
            align-items: center;
            border-bottom: 1px solid rgba(255, 255, 255, 0.1);
            transition: background 0.3s;
        }
        
        .driver-row:hover {
            background: rgba(255, 255, 255, 0.1);
        }
        
        .position {
            font-size: 20px;
            font-weight: bold;
            color: #00ff00;
        }
        
        .car-number {
            font-size: 16px;
            font-weight: bold;
        }
        
        .driver-name {
            font-size: 14px;
        }
        
        .gap {
            font-size: 12px;
            text-align: right;
            color: #aaa;
        }
        
        .last-lap {
            font-size: 12px;
            text-align: right;
        }
        
        .pit-indicator {
            color: #ff9800;
            font-weight: bold;
        }
    </style>
</head>
<body>
    <div class="standings-tower">
        <div class="standings-header">LIVE STANDINGS</div>
        <div id="standings-container"></div>
    </div>

    <script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@latest/dist/browser/signalr.min.js"></script>
    <script>
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("http://localhost:5000/broadcast")
            .withAutomaticReconnect()
            .build();

        let driverCache = new Map();

        connection.on("SessionUpdate", (sessionData) => {
            console.log("Session update received", sessionData);
            
            // Cache driver information
            if (sessionData.drivers) {
                sessionData.drivers.forEach(driver => {
                    driverCache.set(driver.carIdx, driver);
                });
            }
        });

        connection.on("TelemetryUpdate", (data) => {
            updateStandings(data);
        });

        function updateStandings(data) {
            const container = document.getElementById('standings-container');
            container.innerHTML = '';

            data.standings.forEach(entry => {
                const driver = driverCache.get(entry.carIdx);
                if (!driver) return;

                const row = document.createElement('div');
                row.className = 'driver-row';
                
                const positionEl = document.createElement('div');
                positionEl.className = 'position';
                positionEl.textContent = entry.position;
                
                const carNumEl = document.createElement('div');
                carNumEl.className = 'car-number';
                carNumEl.textContent = driver.carNumber || entry.carIdx;
                
                const nameEl = document.createElement('div');
                nameEl.className = 'driver-name';
                nameEl.textContent = driver.userName || 'Unknown';
                if (entry.onPitRoad) {
                    nameEl.innerHTML += ' <span class="pit-indicator">PIT</span>';
                }
                
                const gapEl = document.createElement('div');
                gapEl.className = 'gap';
                if (entry.position === 1) {
                    gapEl.textContent = 'LEADER';
                } else if (entry.gapToLeader != null) {
                    gapEl.textContent = '+' + entry.gapToLeader.toFixed(3);
                }
                
                const lapTimeEl = document.createElement('div');
                lapTimeEl.className = 'last-lap';
                if (entry.lastLapTime != null && entry.lastLapTime > 0) {
                    lapTimeEl.textContent = formatLapTime(entry.lastLapTime);
                }
                
                row.appendChild(positionEl);
                row.appendChild(carNumEl);
                row.appendChild(nameEl);
                row.appendChild(gapEl);
                row.appendChild(lapTimeEl);
                
                container.appendChild(row);
            });
        }

        function formatLapTime(seconds) {
            const mins = Math.floor(seconds / 60);
            const secs = (seconds % 60).toFixed(3);
            return `${mins}:${secs.padStart(6, '0')}`;
        }

        // Start the connection
        connection.start()
            .then(() => {
                console.log("Connected to broadcast hub");
                connection.invoke("SubscribeToStandings");
            })
            .catch(err => console.error("Connection error:", err));
    </script>
</body>
</html>
```

### Step 5: Create Producer Panel (React Example)

```typescript
// ProducerPanel.tsx
import React, { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';

interface OverlaySettings {
    showStandings: boolean;
    showBattleBox: boolean;
    standingsCount: number;
    battleBoxFocus: number[];
}

const ProducerPanel: React.FC = () => {
    const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
    const [settings, setSettings] = useState<OverlaySettings>({
        showStandings: true,
        showBattleBox: false,
        standingsCount: 10,
        battleBoxFocus: []
    });
    const [isConnected, setIsConnected] = useState(false);

    useEffect(() => {
        const newConnection = new signalR.HubConnectionBuilder()
            .withUrl("http://localhost:5000/broadcast")
            .withAutomaticReconnect()
            .build();

        newConnection.start()
            .then(() => {
                console.log("Producer panel connected");
                setIsConnected(true);
            })
            .catch(err => console.error("Connection failed:", err));

        setConnection(newConnection);

        return () => {
            newConnection.stop();
        };
    }, []);

    const updateSetting = (key: keyof OverlaySettings, value: any) => {
        const newSettings = { ...settings, [key]: value };
        setSettings(newSettings);
        
        if (connection && isConnected) {
            connection.invoke("UpdateOverlaySettings", key, value)
                .catch(err => console.error("Failed to update setting:", err));
        }
    };

    return (
        <div className="producer-panel">
            <h1>Broadcast Producer Panel</h1>
            
            <div className="connection-status">
                Status: {isConnected ? '🟢 Connected' : '🔴 Disconnected'}
            </div>

            <div className="control-section">
                <h2>Overlay Controls</h2>
                
                <div className="control-group">
                    <label>
                        <input
                            type="checkbox"
                            checked={settings.showStandings}
                            onChange={(e) => updateSetting('showStandings', e.target.checked)}
                        />
                        Show Standings Tower
                    </label>
                </div>

                <div className="control-group">
                    <label>
                        <input
                            type="checkbox"
                            checked={settings.showBattleBox}
                            onChange={(e) => updateSetting('showBattleBox', e.target.checked)}
                        />
                        Show Battle Box
                    </label>
                </div>

                <div className="control-group">
                    <label>
                        Standings Count:
                        <input
                            type="range"
                            min="5"
                            max="20"
                            value={settings.standingsCount}
                            onChange={(e) => updateSetting('standingsCount', parseInt(e.target.value))}
                        />
                        {settings.standingsCount}
                    </label>
                </div>
            </div>

            <div className="control-section">
                <h2>Battle Box Setup</h2>
                <p>Select cars to feature in battle box comparison</p>
                {/* Car selection interface would go here */}
            </div>
        </div>
    );
};

export default ProducerPanel;
```

## Best Practices

### 1. Throttle Update Rates

```csharp
// DON'T: Send all 60Hz updates to overlay clients
await foreach (var data in client.TelemetryData.WithCancellation(ct))
{
    await _hubContext.Clients.All.SendAsync("Update", data); // Too fast!
}

// DO: Throttle to appropriate rate (10-30Hz typical)
var frameCounter = 0;
await foreach (var data in client.TelemetryData.WithCancellation(ct))
{
    if (++frameCounter % 6 != 0) continue; // 10Hz (every 6th frame)
    await _hubContext.Clients.All.SendAsync("Update", ProcessData(data));
}
```

### 2. Minimize Data Transfer

```csharp
// DON'T: Send entire arrays when most data is unchanged
var allData = new { CarIdxPosition = data.CarIdxPosition };

// DO: Only send data that changed or is relevant
var activeDrivers = GetActiveDrivers(data)
    .Select(idx => new 
    { 
        CarIdx = idx, 
        Position = data.CarIdxPosition[idx],
        LastLap = data.CarIdxLastLapTime[idx]
    });
```

### 3. Handle Reconnections Gracefully

```javascript
// Client-side reconnection with state recovery
connection.onreconnected(() => {
    console.log("Reconnected to server");
    // Request full state refresh
    connection.invoke("RequestFullUpdate");
});

connection.onclose(() => {
    console.log("Connection lost, attempting to reconnect...");
    setTimeout(() => connection.start(), 5000);
});
```

### 4. Cache Driver Information

```csharp
// Cache driver info to avoid sending repeatedly
private Dictionary<int, DriverInfo> _driverCache = new();

private async Task ProcessSessionStream(...)
{
    await foreach (var session in client.SessionData.WithCancellation(ct))
    {
        // Only send driver info when it changes
        var newDrivers = session.DriverInfo?.Drivers
            ?.Where(d => !_driverCache.ContainsKey(d.CarIdx) 
                        || HasDriverChanged(_driverCache[d.CarIdx], d))
            .ToList();

        if (newDrivers?.Any() == true)
        {
            await _hubContext.Clients.All.SendAsync("DriversUpdate", newDrivers);
            newDrivers.ForEach(d => _driverCache[d.CarIdx] = d);
        }
    }
}
```

### 5. Use Groups for Targeted Updates

```csharp
// Send specific data to specific overlay types
await _hubContext.Clients.Group("Standings")
    .SendAsync("StandingsUpdate", standingsData);

await _hubContext.Clients.Group("BattleBox")
    .SendAsync("BattleBoxUpdate", battleBoxData);

await _hubContext.Clients.Group("Timing")
    .SendAsync("TimingUpdate", timingData);
```

## Performance Considerations

### Backend Service Performance

1. **Telemetry Processing**: Keep processing lightweight in the hot path
   ```csharp
   // Fast: Simple data selection
   var standings = data.CarIdxPosition
       ?.Select((pos, idx) => new { idx, pos })
       .Where(x => x.pos > 0)
       .OrderBy(x => x.pos);
   
   // Avoid: Complex calculations in 60Hz loop
   ```

2. **Throttling**: Match update rate to display refresh (typically 10-30Hz)

3. **Async Operations**: Keep SignalR sends non-blocking
   ```csharp
   // Use fire-and-forget for high-frequency updates
   _ = _hubContext.Clients.All.SendAsync("Update", data);
   ```

### Client-Side Performance

1. **DOM Updates**: Batch DOM modifications
   ```javascript
   // DON'T: Update DOM for each driver
   drivers.forEach(d => updateDriverRow(d));
   
   // DO: Build all HTML, then update once
   const html = drivers.map(d => buildDriverRow(d)).join('');
   container.innerHTML = html;
   ```

2. **Use CSS Animations**: Let GPU handle transitions
   ```css
   .driver-row {
       transition: transform 0.3s ease-out;
   }
   ```

3. **Request Animation Frame**: Sync updates with browser rendering
   ```javascript
   let pendingUpdate = null;
   
   connection.on("Update", (data) => {
       pendingUpdate = data;
   });
   
   function render() {
       if (pendingUpdate) {
           updateDisplay(pendingUpdate);
           pendingUpdate = null;
       }
       requestAnimationFrame(render);
   }
   
   requestAnimationFrame(render);
   ```

### Network Optimization

1. **Binary Serialization**: Consider MessagePack for high-frequency data
   ```csharp
   services.AddSignalR()
       .AddMessagePackProtocol();
   ```

2. **Compression**: Enable SignalR compression for large payloads
   ```csharp
   services.AddSignalR(options =>
   {
       options.EnableDetailedErrors = true;
       options.MaximumReceiveMessageSize = 102400; // 100KB
   });
   ```

## Example: Complete Battle Box Implementation

### Backend Calculation

```csharp
private object CreateBattleBoxData(TelemetryData data, int carIdx1, int carIdx2)
{
    if (data.CarIdxPosition == null || 
        data.CarIdxLapDistPct == null) 
        return null;

    var car1Pos = data.CarIdxPosition[carIdx1];
    var car2Pos = data.CarIdxPosition[carIdx2];
    
    var car1LapPct = data.CarIdxLapDistPct[carIdx1] ?? 0;
    var car2LapPct = data.CarIdxLapDistPct[carIdx2] ?? 0;
    
    // Calculate gap in seconds (approximate)
    var lapPctDiff = Math.Abs(car1LapPct - car2LapPct);
    var estimatedGap = lapPctDiff * (data.CarIdxLastLapTime?[carIdx1] ?? 90);

    return new
    {
        Car1 = new
        {
            CarIdx = carIdx1,
            Position = car1Pos,
            LapPct = car1LapPct,
            LastLapTime = data.CarIdxLastLapTime?[carIdx1],
            BestLapTime = data.CarIdxBestLapTime?[carIdx1]
        },
        Car2 = new
        {
            CarIdx = carIdx2,
            Position = car2Pos,
            LapPct = car2LapPct,
            LastLapTime = data.CarIdxLastLapTime?[carIdx2],
            BestLapTime = data.CarIdxBestLapTime?[carIdx2]
        },
        Gap = estimatedGap,
        Leader = car1LapPct > car2LapPct ? carIdx1 : carIdx2
    };
}
```

### Frontend Battle Box Component

```html
<div class="battle-box">
    <div class="car-comparison">
        <div class="car car-1">
            <div class="position">P<span id="car1-pos"></span></div>
            <div class="car-number" id="car1-number"></div>
            <div class="driver-name" id="car1-name"></div>
            <div class="lap-time" id="car1-lap"></div>
        </div>
        
        <div class="gap-display">
            <div class="gap-value" id="gap"></div>
            <div class="gap-label">GAP</div>
        </div>
        
        <div class="car car-2">
            <div class="position">P<span id="car2-pos"></span></div>
            <div class="car-number" id="car2-number"></div>
            <div class="driver-name" id="car2-name"></div>
            <div class="lap-time" id="car2-lap"></div>
        </div>
    </div>
</div>

<script>
connection.on("BattleBoxUpdate", (data) => {
    document.getElementById('car1-pos').textContent = data.car1.position;
    document.getElementById('car1-number').textContent = 
        getDriverInfo(data.car1.carIdx).carNumber;
    document.getElementById('car1-name').textContent = 
        getDriverInfo(data.car1.carIdx).userName;
    document.getElementById('car1-lap').textContent = 
        formatLapTime(data.car1.lastLapTime);
    
    document.getElementById('car2-pos').textContent = data.car2.position;
    document.getElementById('car2-number').textContent = 
        getDriverInfo(data.car2.carIdx).carNumber;
    document.getElementById('car2-name').textContent = 
        getDriverInfo(data.car2.carIdx).userName;
    document.getElementById('car2-lap').textContent = 
        formatLapTime(data.car2.lastLapTime);
    
    document.getElementById('gap').textContent = 
        data.gap.toFixed(3) + 's';
});
</script>
```

## Additional Resources

- **SignalR Documentation**: https://docs.microsoft.com/en-us/aspnet/core/signalr
- **OBS Browser Source**: https://obsproject.com/wiki/Sources-Guide#browsersource
- **vMix Web Input**: https://www.vmix.com/help/inputs/web-input.html
- **iRacing Telemetry SDK AI Usage Guide**: [AI_USAGE.md](./AI_USAGE.md)

## Conclusion

This SDK provides all the necessary tools to build professional broadcast overlay systems:

- **Real-time telemetry access** at 60Hz with 200+ variables
- **Flexible architecture** supporting multiple overlay clients
- **High performance** processing capability
- **Type safety** reducing production errors

The combination of the SDK's high-performance telemetry streaming with SignalR's real-time web communication creates a robust foundation for professional broadcast graphics systems, whether for live streaming, race analysis, or broadcast television production.

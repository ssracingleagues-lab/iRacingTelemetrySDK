# Broadcast Overlay Sample

This sample demonstrates how to build a professional broadcast overlay system using the iRacing Telemetry SDK. It includes:

- **Backend Service**: .NET 8.0 web application that connects to iRacing and broadcasts telemetry via SignalR
- **Overlay Screen**: HTML/JavaScript browser-based overlay showing live standings tower
- **Producer Panel**: HTML/JavaScript control interface for managing overlay settings

## Features

### Standings Tower Overlay
- Real-time position updates (10Hz)
- Driver names, car numbers, and team names
- Gap to leader and last lap times
- Pit road indicators
- Visual highlighting for leader and top 3 positions
- Smooth animations and transitions

### Producer Panel
- Connection status monitoring
- Session information display
- Overlay control toggles
- Battle box car selection
- Driver list view
- Settings broadcast to overlays

## Architecture

```
┌─────────────────────┐
│  iRacing Simulator  │
└──────────┬──────────┘
           │ 60Hz Telemetry
           ▼
┌─────────────────────────────────┐
│  Backend Service (ASP.NET Core) │
│  • TelemetryBackendService      │
│  • SignalR BroadcastHub         │
│  • Throttles to 10Hz            │
└──────────┬──────────────────────┘
           │ SignalR WebSockets
     ┌─────┴─────┐
     ▼           ▼
┌──────────┐ ┌──────────────┐
│ Overlay  │ │ Producer     │
│ Screen   │ │ Panel        │
│ (HTML)   │ │ (HTML)       │
└──────────┘ └──────────────┘
```

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- iRacing simulator (for live data) OR an IBT file (for playback)
- Modern web browser (Chrome, Firefox, Edge)

### Running the Sample

1. **Build and run the backend service:**

   ```bash
   cd Samples/BroadcastOverlay
   dotnet run
   ```

   The service will start on `http://localhost:5000` by default.

2. **Open the producer panel:**

   Navigate to: `http://localhost:5000/producer.html`

   This control interface lets you:
   - Monitor connection status
   - View session information
   - Control overlay settings
   - Select cars for battle box comparison

3. **Open the overlay screen:**

   Navigate to: `http://localhost:5000/overlay.html`

   This is the broadcast graphic that would be captured by OBS, vMix, or similar software.

   **For OBS Studio:**
   - Add a "Browser" source
   - Set URL to: `http://localhost:5000/overlay.html`
   - Set width: 1920, height: 1080
   - Check "Shutdown source when not visible"
   - Optionally add chroma key filter if using transparent background

4. **Start iRacing:**

   Once iRacing is running and you're in a session (practice, qualifying, or race), the overlay will automatically populate with live data.

### Using IBT File Playback

To test without iRacing running, modify `TelemetryBackendService.cs`:

```csharp
// Replace this line:
await using var client = TelemetryClient<TelemetryData>.Create(_logger);

// With:
var ibtOptions = new IBTOptions(@"C:\path\to\your\file.ibt", playBackSpeedMultiplier: 10);
await using var client = TelemetryClient<TelemetryData>.Create(_logger, ibtOptions);
```

## Customization

### Adjusting Update Rate

In `TelemetryBackendService.cs`, modify the throttling:

```csharp
// Current: 10Hz (every 6th frame)
if (++frameCounter % 6 != 0) continue;

// For 20Hz: every 3rd frame
if (++frameCounter % 3 != 0) continue;

// For 30Hz: every 2nd frame
if (++frameCounter % 2 != 0) continue;
```

### Customizing Overlay Appearance

Edit `wwwroot/overlay.html` to modify:
- Colors and styling (CSS)
- Layout and positioning
- Animation effects
- Data display format

### Adding More Telemetry Variables

To add more telemetry data:

1. Update the `[RequiredTelemetryVars]` attribute in `TelemetryBackendService.cs`
2. Modify `CreateBroadcastData()` method to include new data
3. Update `overlay.html` to display the new data

Example - Adding fuel information:

```csharp
[RequiredTelemetryVars([
    // ... existing vars ...
    TelemetryVar.FuelLevel,
    TelemetryVar.FuelUsePerHour
])]
```

Then in `CreateBroadcastData()`:

```csharp
return new
{
    // ... existing data ...
    FuelLevel = data.FuelLevel,
    FuelUsePerHour = data.FuelUsePerHour
};
```

## Network Configuration

### Allowing Remote Connections

To access overlays from other devices on your network:

1. Update CORS policy in `Program.cs`:
   ```csharp
   policy.WithOrigins("http://*:3000", "http://*:5173")
         .SetIsOriginAllowedToAllowWildcardSubdomains()
   ```

2. Update SignalR URL in HTML files:
   ```javascript
   .withUrl("http://YOUR_SERVER_IP:5000/broadcast")
   ```

3. Configure firewall to allow port 5000

### HTTPS Support

For production use, enable HTTPS:

1. Generate development certificate:
   ```bash
   dotnet dev-certs https --trust
   ```

2. Update `Program.cs` to use HTTPS URLs

3. Update overlay/producer HTML files to use `https://` URLs

## Performance Tips

1. **Limit Overlay Complexity**: Keep DOM updates simple for smooth rendering
2. **Use CSS Animations**: GPU-accelerated CSS is faster than JavaScript animations
3. **Throttle Updates**: 10-20Hz is usually sufficient for smooth visuals
4. **Minimize Data Transfer**: Only send changed data when possible
5. **Use Binary Protocols**: Consider MessagePack for high-frequency data

## Troubleshooting

### "Connection failed" in browser console

- Verify backend service is running
- Check that firewall isn't blocking port 5000
- Ensure correct URL in browser (localhost vs 127.0.0.1)

### Overlay shows "Waiting for telemetry data..."

- Confirm iRacing is running and in a session
- Check backend service logs for connection errors
- Verify telemetry client is connecting (check console output)

### Performance issues / stuttering

- Reduce update frequency (increase throttle value)
- Simplify overlay HTML/CSS
- Close unnecessary browser tabs
- Check CPU usage of backend service

## Related Documentation

- [Broadcast Overlay Guide](../../Sdk/SVappsLAB.iRacingTelemetrySDK/contents/docs/BROADCAST_OVERLAY_GUIDE.md) - Comprehensive implementation guide
- [AI Usage Guide](../../Sdk/SVappsLAB.iRacingTelemetrySDK/contents/docs/AI_USAGE.md) - SDK usage patterns and best practices
- [SignalR Documentation](https://docs.microsoft.com/en-us/aspnet/core/signalr) - Microsoft's SignalR guide

## License

This sample is licensed under the Apache License 2.0, same as the parent SDK.

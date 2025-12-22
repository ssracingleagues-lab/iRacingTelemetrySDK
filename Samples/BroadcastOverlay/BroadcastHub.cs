using Microsoft.AspNetCore.SignalR;
using SVappsLAB.iRacingTelemetrySDK;

namespace BroadcastOverlay;

/// <summary>
/// SignalR hub for broadcasting telemetry data to overlay clients and producer panels
/// </summary>
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

    /// <summary>
    /// Subscribe client to standings tower updates
    /// </summary>
    public async Task SubscribeToStandings()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "Standings");
        _logger.LogInformation("Client {ConnectionId} subscribed to standings", Context.ConnectionId);
    }

    /// <summary>
    /// Subscribe client to battle box updates
    /// </summary>
    public async Task SubscribeToBattleBox()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "BattleBox");
        _logger.LogInformation("Client {ConnectionId} subscribed to battle box", Context.ConnectionId);
    }

    /// <summary>
    /// Producer panel can send control commands to update overlay settings
    /// </summary>
    public async Task UpdateOverlaySettings(string settingName, object value)
    {
        _logger.LogInformation("Overlay setting updated: {Setting} = {Value}", settingName, value);
        
        // Broadcast setting change to all overlay clients (but not back to sender)
        await Clients.Others.SendAsync("SettingChanged", settingName, value);
    }

    /// <summary>
    /// Producer panel requests to focus battle box on specific cars
    /// </summary>
    public async Task SetBattleBoxFocus(int carIdx1, int carIdx2)
    {
        _logger.LogInformation("Battle box focus set to cars {Car1} and {Car2}", carIdx1, carIdx2);
        await Clients.All.SendAsync("BattleBoxFocusChanged", carIdx1, carIdx2);
    }

    /// <summary>
    /// Request full state refresh (useful after reconnection)
    /// </summary>
    public async Task RequestFullUpdate()
    {
        _logger.LogInformation("Client {ConnectionId} requested full update", Context.ConnectionId);
        // The backend service will handle this by sending current state
        await Clients.Caller.SendAsync("FullUpdateRequested");
    }
}
